﻿// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices.TestFramework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Windows.Networking.PushNotifications;
using System;
using System.Xml.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Test
{
    [Tag("push")]
    public class PushFunctional : FunctionalTestBase
    {
        private static Queue<PushNotificationReceivedEventArgs> pushesReceived = new Queue<PushNotificationReceivedEventArgs>();
        public const string PushTestTableName = "w8PushTest";

        [AsyncTestMethod]
        public async Task InitialDeleteRegistrationsAsync()
        {
            string channelUri = await this.GetChannelUri();
            Dictionary<string, string> channelUriParam = new Dictionary<string, string>()
            {
                {"channelUri", channelUri}
            };
            await this.GetClient().InvokeApiAsync("deleteRegistrationsForChannel", HttpMethod.Delete, channelUriParam);
        }

        [AsyncTestMethod]
        public async Task RegisterAsync()
        {
            string channelUri = await this.GetChannelUri();
            Dictionary<string, string> parameters = new Dictionary<string, string>()
            {
                {"channelUri", channelUri}
            };
            var push = this.GetClient().GetPush();
            await push.RegisterAsync(channelUri);
            await VerifyRegistration(parameters, push);
        }

        [AsyncTestMethod]
        public async Task LoginRegisterAsync()
        {
            string channelUri = await this.GetChannelUri();
            MobileServiceUser user = await Utilities.GetDummyUser(this.GetClient());
            this.GetClient().CurrentUser = user;
            Dictionary<string, string> parameters = new Dictionary<string, string>()
            {
                {"channelUri", channelUri}
            };
            var push = this.GetClient().GetPush();
            await push.RegisterAsync(channelUri);
            await VerifyRegistration(parameters, push);
        }

        [AsyncTestMethod]
        public async Task UnregisterAsync()
        {
            var push = this.GetClient().GetPush();
            await push.UnregisterAsync();
            await this.GetClient().InvokeApiAsync("verifyUnregisterInstallationResult", HttpMethod.Get, null);
        }

        [AsyncTestMethod]
        public async Task RegisterAsyncWithTemplatesAndSecondaryTiles()
        {
            string channelUri = await this.GetChannelUri();
            JObject templates = GetTemplates("bar");
            JObject secondaryTiles = GetSecondaryTiles(channelUri, "foo");
            var push = this.GetClient().GetPush();
            await push.RegisterAsync(channelUri, templates, secondaryTiles);
            JObject expectedTemplates = GetTemplates(null);
            JObject expectedSecondaryTiles = GetSecondaryTiles(channelUri, null, true);
            Dictionary<string, string> parameters = new Dictionary<string, string>()
            {
                {"channelUri", channelUri},
                {"templates", JsonConvert.SerializeObject(expectedTemplates)},
                {"secondaryTiles", JsonConvert.SerializeObject(expectedSecondaryTiles)}
            };
            await VerifyRegistration(parameters, push);
        }

        [AsyncTestMethod]
        public async Task RegisterAsyncTemplatesAndOverride()
        {
            var channelUri = await this.GetChannelUri();
            JObject templates = GetTemplates("foo");
            JObject expectedTemplates = GetTemplates(null);
            var push = this.GetClient().GetPush();
            try
            {
                await push.RegisterAsync(channelUri);
                Dictionary<string, string> parameters = new Dictionary<string, string>()
                {
                    {"channelUri", channelUri},
                };
                await this.GetClient().InvokeApiAsync("verifyRegisterInstallationResult", HttpMethod.Get, parameters);

                await push.RegisterAsync(channelUri, templates);
                parameters = new Dictionary<string, string>()
                {
                    {"channelUri", channelUri},
                    {"templates", JsonConvert.SerializeObject(expectedTemplates)},
                };

                await this.GetClient().InvokeApiAsync("verifyRegisterInstallationResult", HttpMethod.Get, parameters);

                await push.RegisterAsync(channelUri);
                parameters = new Dictionary<string, string>()
                {
                    {"channelUri", channelUri},
                };
                await this.GetClient().InvokeApiAsync("verifyRegisterInstallationResult", HttpMethod.Get, parameters);
            }
            finally
            {
                push.UnregisterAsync().Wait();
                this.GetClient().CurrentUser = null;
            }
        }

        private async Task VerifyRegistration(Dictionary<string, string> parameters, Push push)
        {
            try
            {
                await this.GetClient().InvokeApiAsync("verifyRegisterInstallationResult", HttpMethod.Get, parameters);
            }
            finally
            {
                push.UnregisterAsync().Wait();
                this.GetClient().CurrentUser = null;
            }
        }

        //Temporarily exclude this one from test automation
        //[AsyncTestMethod]
        public async Task WnsToastPushTest()
        {
            PushWatcher watcher = new PushWatcher();
            var client = this.GetClient();
            var push = this.GetClient().GetPush();
            string pushChannelUri = await GetChannelUri();

            //Build push payload
            XElement expectedResult = BuildXmlToastPayload("sendToastText01", "Hello World");
            var xmlPayload = "<?xml version=\"1.0\"?>" + expectedResult;
            JObject body = new JObject();
            body["method"] = "send";
            body["type"] = "wns";
            body["payload"] = xmlPayload;
            body["token"] = "dummy";
            body["wnsType"] = "toast";

            try
            {
                //Register for Push
                await push.RegisterAsync(pushChannelUri);
                //Invoke API to send push
                await this.GetClient().InvokeApiAsync("push", body);
                var notificationResult = await watcher.WaitForPush(TimeSpan.FromSeconds(10));
                if (notificationResult == null)
                {
                    Assert.Fail("Error, push not received on the timeout allowed");
                }
                else
                {
                    Log("Push notification received:");
                    XElement receivedPushInfo = XElement.Parse(notificationResult.ToastNotification.Content.GetXml());
                    Log("  {0}: {1}", notificationResult.NotificationType, receivedPushInfo);

                    if (expectedResult.ToString(SaveOptions.DisableFormatting) == receivedPushInfo.ToString(SaveOptions.DisableFormatting))
                    {
                        Log("Received notification is the expected one.");
                    }
                    else
                    {
                        Assert.Fail(string.Format("Received notification is not the expected one. \r\nExpected:{0} \r\nActual:{1}", expectedResult.ToString(), receivedPushInfo.ToString()));
                    }
                }
            }
            finally
            {
                push.UnregisterAsync().Wait();
            }
        }

        private static JObject GetTemplates(string tag)
        {
            var toastTemplate = "<toast><visual><binding template=\"ToastText01\"><text id=\"1\">$(message)</text></binding></visual></toast>";
            JObject templateBody = new JObject();
            templateBody["body"] = toastTemplate;

            JObject wnsToastHeaders = new JObject();
            wnsToastHeaders["X-WNS-Type"] = "wns/toast";
            templateBody["headers"] = wnsToastHeaders;
            if (tag != null)
            {
                JArray tags = new JArray();
                tags.Add(tag);
                templateBody["tags"] = tags;
            }
            JObject templates = new JObject();
            templates["testTemplate"] = templateBody;
            return templates;
        }

        private static JObject GetSecondaryTiles(string channelUri, string tag, bool expectedTiles = false)
        {
            JObject secondaryTileBody = new JObject();
            secondaryTileBody["pushChannel"] = channelUri;
            if (expectedTiles)
            {
                secondaryTileBody["pushChannelExpired"] = false;
            }
            JArray tags = new JArray();
            tags.Add("bar");
            JObject templates = GetTemplates(tag);
            secondaryTileBody["templates"] = templates;
            JObject secondaryTiles = new JObject();
            secondaryTiles["testSecondaryTiles"] = secondaryTileBody;
            return secondaryTiles;
        }

        private async Task<string> GetChannelUri()
        {
            var pushChannel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
            pushChannel.PushNotificationReceived += pushChannel_PushNotificationReceived;
            return pushChannel.Uri;
        }

        private static XElement BuildXmlToastPayload(string wnsMethod, string text1)
        {
            XElement binding = new XElement("binding", new XAttribute("template", wnsMethod.Substring("send".Length)));

            binding.Add(new XElement("text", new XAttribute("id", 1), new XText(text1)));

            XElement xmlPayload = new XElement("toast",
                new XElement("visual",
                    binding));
            return xmlPayload;
        }

        static void pushChannel_PushNotificationReceived(PushNotificationChannel sender, PushNotificationReceivedEventArgs args)
        {
            pushesReceived.Enqueue(args);
        }

        class PushWatcher
        {
            public async Task<PushNotificationReceivedEventArgs> WaitForPush(TimeSpan maximumWait)
            {
                PushNotificationReceivedEventArgs result = null;
                var tcs = new TaskCompletionSource<PushNotificationReceivedEventArgs>();
                DateTime start = DateTime.UtcNow;
                while (DateTime.UtcNow.Subtract(start) < maximumWait)
                {
                    if (pushesReceived.Count > 0)
                    {
                        result = pushesReceived.Dequeue();
                        break;
                    }

                    await Task.Delay(500);
                }

                return result;
            }
        }
    }
}