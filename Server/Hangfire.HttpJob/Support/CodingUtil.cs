﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Hangfire.Dashboard;
using Hangfire.Logging;
using Newtonsoft.Json;
using Spring.Core.TypeConversion;

namespace Hangfire.HttpJob.Support
{
    public class CodingUtil
    {
        private static readonly ILog Logger = LogProvider.For<CodingUtil>();

        /// <summary>
        ///判断是否引用了tag服务
        /// </summary>
        public static bool IsTagServiceInstalled = DashboardRoutes.Routes.FindDispatcher("/tags/all") != null || DashboardRoutes.Routes.FindDispatcher("/tags/search") != null;

        /// <summary>
        /// 启动配置
        /// </summary>
        public static HangfireHttpJobOptions HangfireHttpJobOptions = new HangfireHttpJobOptions();


        /// <summary>
        /// appsettions.json配置文件最后更新时间
        /// </summary>
        private static DateTime? _appJsonLastWriteTime;

        /// <summary>
        /// appsettions.json配置文件内容
        /// </summary>
        internal static Dictionary<string, object> _appsettingsJson = new Dictionary<string, object>();

        /// <summary>
        /// 全局配置 每次会检测是否有改变
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, object> GetGlobalAppsettings()
        {
            var jsonFile = new FileInfo(HangfireHttpJobOptions.GlobalSettingJsonFilePath);
            if (jsonFile.Exists && (_appJsonLastWriteTime == null || _appJsonLastWriteTime != jsonFile.LastWriteTime))
            {
                _appJsonLastWriteTime = jsonFile.LastWriteTime;
                try
                {
                    _appsettingsJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(jsonFile.FullName));
                }
                catch (Exception e)
                {
                    Logger.WarnException($"HangfireHttpJobOptions.GlobalSettingJsonFilePath read fail", e);
                }
            }

            return _appsettingsJson??new Dictionary<string, object>();
        }

        /// <summary>
        /// 获取job的执行url页面可以查看日志等
        /// </summary>
        /// <param name="jobId"></param>
        /// <returns></returns>
        public static string GetCurrentJobDetailUrl(string jobId)
        {
            //优先使用全局配置里面的参数
            CodingUtil.GetGlobalAppsettings().TryGetValue("CurrentDomain", out var currentDomain);

            var logDetail = currentDomain != null && !string.IsNullOrEmpty(currentDomain.ToString()) ? $"{currentDomain}/jobs/details/{jobId}" : string.IsNullOrEmpty(CodingUtil.HangfireHttpJobOptions.CurrentDomain) ? $"JobId:{jobId}" : $"{CodingUtil.HangfireHttpJobOptions.CurrentDomain}/jobs/details/{jobId}";

            return logDetail;
        }

        /// <summary>
        /// JobAgent的单例模式 当没有执行完重复执行是否需要视为错误对待
        /// </summary>
        /// <returns></returns>
        public static bool IgnoreJobAgentSingletonMultExcuteError()
        {
            //优先使用全局配置里面的参数
            return CodingUtil.GetGlobalAppsettings().TryGetValue("IgnoreJobAgentSingletonMultExcuteError", out var value) && value is bool dd && dd ;  
        }

        /// <summary>
        /// 钉钉错误内容通知默认用Exception.ToString 如果这个设置为true 那么只会用Exception.Message
        /// </summary>
        /// <returns></returns>
        public static bool DingTalkErrReportSimplify()
        {
            return CodingUtil.GetGlobalAppsettings().TryGetValue("EnableDingTalkErrReportSimplify", out var value) && value is bool dd && dd;
        }

        /// <summary>
        /// 配置设置job的过期时间单位是天
        /// </summary>
        /// <returns></returns>
        public static long JobTimeoutDays()
        {
            var timeoutDays = 0L;
            //如果在全局配置页面有配置的话优先使用这个配置
            if (!GetGlobalAppsettings().TryGetValue("JobTimeoutDays", out var value))
            {
                timeoutDays = 0L;
            }
            else
            {
                if (value is long)
                {
                    timeoutDays = (long)value;
                }
            }

            return timeoutDays > 0 ? timeoutDays :
                HangfireHttpJobOptions.JobExpirationTimeoutDay < 1 ? 1L : HangfireHttpJobOptions.JobExpirationTimeoutDay;
        }

        /// <summary>
        /// 获取动态的全局配置
        /// </summary>
        /// <param name="value"></param>
        /// <param name="deflaultValue"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetGlobalAppsetting<T>(string value, T deflaultValue)
        {
            try
            {
                if (_appsettingsJson!=null && _appsettingsJson.TryGetValue(value, out var v))
                {
                    return (T)TypeConversionUtils.ConvertValueIfNecessary(typeof(T), v, null);
                }
            }
            catch (Exception)
            {
                //ignore
            }
            return deflaultValue;
        }

        /// <summary>
        /// 获取代理配置 先检查有没有在全局json里面动态配置 再检查有没有在启动的json文件里有配置
        /// </summary>
        /// <param name="proxy"></param>
        /// <returns></returns>
        public static bool TryGetGlobalProxy(out string proxy)
        {
            proxy = GetGlobalAppsetting("GlobalProxy", "");
            if (string.IsNullOrEmpty(proxy))
            {
                proxy = HangfireHttpJobOptions.Proxy;
            }

            return !string.IsNullOrEmpty(proxy);
        }
        
        /// <summary>
        /// MD5函数
        /// </summary>
        /// <param name="str">原始字符串</param>
        /// <returns>MD5结果</returns>
        public static string MD5(string str)
        {
            byte[] b = Encoding.UTF8.GetBytes(str);
            b = new MD5CryptoServiceProvider().ComputeHash(b);
            string ret = string.Empty;
            for (int i = 0; i < b.Length; i++)
            {
                ret += b[i].ToString("x").PadLeft(2, '0');
            }
            return ret;
        }

        public static T FromJson<T>(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception)
            {
                return default(T);
            }
        }
    }
}
