﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using BugSplatDotNetStandard.Utils;

namespace BugSplatDotNetStandard
{
    /// <summary>
    /// A class for uploading Exceptions and minidump files to BugSplat
    /// </summary>
    public class BugSplat
    {
        /// <summary>
        /// A default description added to the upload that can be overriden at post time
        /// </summary>
        public string Description { private get; set; } = string.Empty;

        /// <summary>
        /// A default email added to the upload that can be overriden at post time
        /// </summary>
        public string Email { private get; set; } = string.Empty;

        /// <summary>
        /// A default key added to the upload that can be overriden at post time
        /// </summary>
        public string Key { private get; set; } = string.Empty;

        /// <summary>
        /// A default user added to the upload that can be overriden at post time
        /// </summary>
        public string User { private get; set; } = string.Empty;

        private const string CRASH_TYPE_ID_DOT_NET_STANDARD = "18";
        private readonly string database;
        private readonly string application;
        private readonly string version;
        private readonly List<FileInfo> files = new List<FileInfo>();

        /// <summary>
        /// Post Exceptions and minidump files to BugSplat
        /// </summary>
        /// <param name="database">The BugSplat database for your organization</param>
        /// <param name="application">Your application's name (must match value used to upload symbols)</param>
        /// <param name="version">Your application's version (must match value used to upload symbols)</param>
        public BugSplat(string database, string application, string version)
        {
            this.database = database;
            this.application = application;
            this.version = version;
        }

        /// <summary>
        /// Post an Exception to BugSplat
        /// </summary>
        /// <param name="ex">The Exception that will be serialized and posted to BugSplat</param>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        public async Task<HttpResponseMessage> Post(Exception ex, BugSplatPostOptions options = null)
        {
            using (var httpClient = new HttpClient())
            {
                var uri = new Uri($"https://{database}.bugsplat.com/post/dotnetstandard/");
                var callstack = ex.ToString();
                var body = CreateMultiPartFormDataContent(options);
                var description = BugSplatUtils.GetStringValueOrDefault(options?.Description, Description);
                // TODO BG https://github.com/BugSplat-Git/webroot/issues/458
                body.Add(new StringContent(description), "usercomments");
                body.Add(new StringContent(callstack), "callstack");
                body.Add(new StringContent(CRASH_TYPE_ID_DOT_NET_STANDARD), "crashTypeId");

                return await httpClient.PostAsync(uri, body);
            }
        }

        /// <summary>
        /// Post a minidump file to BugSplat
        /// </summary>
        /// <param name="ex">The minidump file that will be posted to BugSplat</param>
        /// <param name="options">Optional parameters that will override the defaults if provided</param>
        public async Task<HttpResponseMessage> Post(FileInfo minidumpFileInfo, BugSplatPostOptions options = null)
        {
            using (var httpClient = new HttpClient())
            {
                var uri = new Uri($"https://{database}.bugsplat.com/api/upload/manual/crash.php");
                var minidump = File.ReadAllBytes(minidumpFileInfo.FullName);
                var body = CreateMultiPartFormDataContent(options);
                body.Add(new ByteArrayContent(minidump), "minidump", "minidump.dmp");

                return await httpClient.PostAsync(uri, body);
            }
        }

        /// <summary>
        /// Add additional files to be sent when Post is called
        /// </summary>
        /// <param name="fileInfo">The file that will be posted to BugSplat</param>
        public void AttachFile(FileInfo fileInfo)
        {
            files.Add(fileInfo);
        }

        private MultipartFormDataContent CreateMultiPartFormDataContent(BugSplatPostOptions options = null)
        {
            var additionalFormDataParams = options?.AdditionalFormDataParams ?? new List<KeyValuePair<string, HttpContent>>();
            var description = BugSplatUtils.GetStringValueOrDefault(options?.Description, Description);
            var email = BugSplatUtils.GetStringValueOrDefault(options?.Email, Email);
            var key = BugSplatUtils.GetStringValueOrDefault(options?.Key, Key);
            var user = BugSplatUtils.GetStringValueOrDefault(options?.User, User);
            
            var body = new MultipartFormDataContent
                {
                    { new StringContent(database), "database" },
                    { new StringContent(application), "appName" },
                    { new StringContent(version), "appVersion" },
                    { new StringContent(description), "description" },
                    { new StringContent(email), "email" },
                    { new StringContent(key), "appKey" },
                    { new StringContent(user), "user" },
                };

            foreach (var param in additionalFormDataParams)
            {
                body.Add(param.Value, param.Key);
            }

            for (var i = 0; i < files.Count; i++)
            {
                var name = files[i].Name;
                var bytes = File.ReadAllBytes(files[i].FullName);
                var contents = Convert.ToBase64String(bytes);
                body.Add(new StringContent(name), $"fileName{i + 1}");
                body.Add(new StringContent(contents), $"optFile{i + 1}");
            }

            return body;
        }
    }
}
