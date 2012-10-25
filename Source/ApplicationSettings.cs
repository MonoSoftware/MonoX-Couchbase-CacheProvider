using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using MonoSoftware.Core;

namespace MonoSoftware.MonoX.Caching
{
    /// <summary>
    /// Application settings.
    /// </summary>
    public static class ApplicationSettings
    {
        #region Properties
        private static string _cacheName = String.Empty;
        /// <summary>
        /// Gets the Cache Name.
        /// </summary>
        public static string CacheName
        {
            get
            {
                if (String.IsNullOrEmpty(_cacheName))
                    try
                    {
                        _cacheName = WebConfigurationManager.AppSettings["CacheName"];
                    }
                    catch {}                    
                return _cacheName;
            }
        }

        private static string _cacheNamePassword = String.Empty;
        /// <summary>
        /// Gets the Cache Name password.
        /// </summary>
        public static string CacheNamePassword
        {
            get
            {
                if (String.IsNullOrEmpty(_cacheNamePassword))
                    try
                    {
                        _cacheNamePassword = WebConfigurationManager.AppSettings["CacheNamePassword"];
                    }
                    catch { }
                return _cacheNamePassword;
            }
        }
        #endregion
    }
}