using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MonoSoftware.Core;
using MonoSoftware.Web.Caching;
using System.Collections;
using Couchbase;
using Enyim.Caching.Memcached;
using Enyim;

namespace MonoSoftware.MonoX.Caching
{
    public class CouchbaseCacheProvider : ICacheProvider, IDisposable
    {
        #region Fields

        static readonly char[] ForbiddenChars = { 
			'\u0000', '\u0001', '\u0002', '\u0003',
			'\u0004', '\u0005', '\u0006', '\u0007',
			'\u0008', '\u0009', '\u000a', '\u000b',
			'\u000c', '\u000d', '\u000e', '\u000f',
			'\u0010', '\u0011', '\u0012', '\u0013',
			'\u0014', '\u0015', '\u0016', '\u0017',
			'\u0018', '\u0019', '\u001a', '\u001b',
			'\u001c', '\u001d', '\u001e', '\u001f',
			'\u0020'
		};

        protected static object padLock = new object();
        protected static object padLockInit = new object();
        protected static object padLockKeys = new object();
        protected static object padLockKeysAction = new object();

        /// <summary>
        /// Couchbase Cache Key maximum length.
        /// </summary>
        protected const int CacheKeyMaxLen = 230;

        /// <summary>
        /// Default AppFabric CacheName preset from AppSettings.
        /// </summary>
        protected string CacheName = ApplicationSettings.CacheName;
        /// <summary>
        /// Default AppFabric CacheName password preset from AppSettings.
        /// </summary>
        protected string CacheNamePassword = ApplicationSettings.CacheNamePassword;
        #endregion

        #region Properties        
        protected static volatile CouchbaseClient _cache = null;
        /// <summary>
        /// Gets the Couchbase Cache.
        /// </summary>
        protected virtual CouchbaseClient Cache
        {
            get
            {
                if (_cache == null)
                {
                    lock (padLockInit)
                    {
                        if (_cache == null)
                        {
                            _cache = new CouchbaseClient(CacheName, CacheNamePassword);
                        }
                    }
                }
                return _cache;
            }
            private set
            {
                lock (padLockInit)
                {
                    _cache = value;
                }
            }
        }

        private static List<string> _keys = null;
        private List<string> Keys
        {
            get
            {
                if (_keys == null)
                {
                    lock (padLockKeys)
                    {
                        if (_keys == null)
                        {
                            _keys = new List<string>();
                        }
                    }
                }
                return _keys;
            }            
        }

        private CacheItemPriorityLevel _priortiy = CacheItemPriorityLevel.Normal;
        /// <summary>
        /// Gets or sets cache item priority level.
        /// <para>
        /// Note: Default is <see cref="CacheItemPriorityLevel.AboveNormal"/>
        /// </para>
        /// </summary>
        public CacheItemPriorityLevel Priortiy
        {
            get
            {
                return _priortiy;
            }
            set
            {
                _priortiy = value;
            }
        } 

        private int _timeout = 0;
        /// <summary>
        /// Gets or sets the cache timeout period in seconds.
        /// <para>
        /// Note: Default is zero.
        /// </para>
        /// </summary>
        public int Timeout
        {
            get { return _timeout; }
            set { _timeout = value; }
        } 
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor.
        /// </summary>
        public CouchbaseCacheProvider()            
        {
        }         
        #endregion              

        #region Methods
        /// <summary>
        /// Gets the optimized Couchbase Cache Key.
        /// </summary>
        /// <param name="key">Original Key.</param>
        /// <returns>Optimized Key.</returns>
        protected virtual string GetCouchBaseCacheKey(string key)
        {
            //NOTE: We can't use the Couchbase Key Transformators because MonoX needs to have delimited keys for partial and parent removals
            StringBuilder result = new StringBuilder(key);
            if (key.Length > CacheKeyMaxLen)
            {
                result = new StringBuilder(String.Format("{0}{1}", result.Substring(0, CacheKeyMaxLen), ((ulong)result.Substring(CacheKeyMaxLen, key.Length - CacheKeyMaxLen).GetHashCode()).ToString()));
            }
            
            if (result.ToString().IndexOfAny(ForbiddenChars) > -1)
            {
                foreach (var item in ForbiddenChars)
                {
                    if (result.IndexOf(item) > -1)
                        result = result.Replace(item, '0');
                }
            }
            return result.ToString();
        }
        #endregion

        #region ICacheProvider

        /// <summary>
        /// Stores the item in the repository based on the key.
        /// </summary>
        /// <param name="key">Key of the item that should be stored.</param>
        /// <param name="data">Item data</param>
        public void Store(string key, object data)
        {
            if (this.Timeout > 0 && data != null)
            {                
                TimeSpan expiresOnSlide = TimeSpan.FromSeconds(this.Timeout);
                Cache.Store(StoreMode.Set, GetCouchBaseCacheKey(key), data, expiresOnSlide);                    
                lock (padLockKeysAction)
                {
                    if (!Keys.Contains(GetCouchBaseCacheKey(key)))
                        Keys.Add(GetCouchBaseCacheKey(key));
                }
            }
        }

        /// <summary>
        /// Removes the item from the repository.
        /// </summary>
        /// <param name="key">Key of the item that should be removed.</param>
        public void Remove(string key)
        {
            Cache.Remove(GetCouchBaseCacheKey(key));                
            lock (padLockKeysAction)
            {
                if (Keys.Contains(GetCouchBaseCacheKey(key)))
                    Keys.Remove(GetCouchBaseCacheKey(key));
            }
        }

        /// <summary>
        /// Removes all the items from the repository.
        /// </summary>
        /// <param name="key">Key of the item that should be removed.</param>
        public void RemoveAll(string key)
        {
            List<string> toRemove = null;
            lock (padLockKeysAction)
                toRemove = new List<string>(Keys.Where(p => p.ToLowerInvariant().StartsWith(GetCouchBaseCacheKey(key).ToLowerInvariant())));

            foreach (string item in toRemove)
            {
                Cache.Remove(item);
                lock (padLockKeysAction)
                {
                    if (Keys.Contains(item))
                        Keys.Remove(item);
                }
            }
        }

        /// <summary>
        /// Retrieves the item from the repository.
        /// </summary>
        /// <typeparam name="T">Type of the item to be retrieved.</typeparam>
        /// <param name="key">Key of the item to be retrieved.</param>
        /// <returns>The object from the cache with the key that is passed as a parameter.</returns>
        public T Get<T>(string key)
        {
            return Cache.Get<T>(GetCouchBaseCacheKey(key));
        }

        /// <summary>
        /// Retrieves the item from the repository.
        /// </summary>
        /// <param name="key">Key of the item to be retrieved.</param>
        /// <returns>The object from the cache with the key that is passed as a parameter.</returns>
        public object Get(string key)
        {
            return Cache.Get(GetCouchBaseCacheKey(key));
        }  
        #endregion

        public void Dispose()
        {
            
        }
    }
}
