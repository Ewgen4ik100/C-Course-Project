using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Collections.Specialized;
namespace LessonBot
{
    class Api
    {
        string Token;
        public Api(string token)
        {
            this.Token = token;
        }
        public JToken CallMethod(string methodName, Dictionary<string, string> parameters)
        {
            string address = $"https://api.vk.com/method/{methodName}?";
            NameValueCollection postParameters = new NameValueCollection();
            foreach(KeyValuePair<string, string> parameter in parameters)
            {
                postParameters.Add(parameter.Key, parameter.Value);
            }
            postParameters.Add("access_token", Token);
            postParameters.Add("v", "5.80");
            JToken response;
            try
            {
                response = JToken.Parse(Request(address, postParameters).ToString());
            } catch { throw new Error.RequestException(); }
            if(response["error"] == null)
            {
                return response["response"];
            }
            else
            {
                if(Convert.ToInt32(response["error"]["error_code"]) == 5)
                {
                    throw new Error.ApiException(response["error"]["error_code"].ToString(), Error.ErrorCode.AccessToken);
                }
                else
                {
                    throw new Error.ApiException(response["error"]["error_code"].ToString(), Error.ErrorCode.Other);
                }
            }
        }
        public string Request(string address)
        {
            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                return client.DownloadString(address);
            }
        }
        public string Request(string address, NameValueCollection parametrs)
        {
            using (var client = new WebClient())
                return Encoding.UTF8.GetString(client.UploadValues(address, parametrs));
        }
        public class LongPoll
        {
            public string Ts;
            public string Key;
            public string Server;
            private Api VKApi;
            public LongPoll(Api vkApi)
            {
                VKApi = vkApi;
                GetInfoLongPoll();
            }
            public List<EventObject> Listen()
            {
                while(true)
                {
                    JToken response = RequestLongPoll();
                    if (Convert.ToInt32(response["failed"]) == 1)
                    {
                        Ts = response["ts"].ToString();
                    }
                    else if (Convert.ToInt32(response["failed"]) == 2 || Convert.ToInt32(response["failed"]) == 3)
                    {
                        GetInfoLongPoll();
                    }
                    else
                    {
                        Ts = response["ts"].ToString();
                        List<EventObject> updates = new List<EventObject>();
                        foreach (var update in response["updates"])
                        {
                            updates.Add(new EventObject(update));
                        }
                        return updates;
                    }
                }
            }
            private JToken RequestLongPoll()
            {
                NameValueCollection parameters = new NameValueCollection() {
                    {"act", "a_check"},
                    {"key", Key},
                    {"ts", Ts},
                    {"wait", "25"},
                    {"mode", "2"},
                    {"version", "3"}
                };
                return JToken.Parse(VKApi.Request($"https://{Server}", parameters));
            }
            private void GetInfoLongPoll()
            {
                JToken infoLongPoll = VKApi.CallMethod("messages.getLongPollServer", new Dictionary<string, string>(1) {
                    {"lp_version", "3"}
                });
                Ts = infoLongPoll["ts"].ToString();
                Key = infoLongPoll["key"].ToString();
                Server = infoLongPoll["server"].ToString();
            }
            public class EventObject
            {
                public VKEventType Type = VKEventType.Other;
                public long MessageId = 0;
                public long PeerId = 0;
                public long Time = 0;
                public string Message = "";
                public JToken Extra;
                public JToken Attachments;
                public bool FromUser = false;
                public bool FromChat = false;
                public bool FromGroup = false;
                public long UserId = 0;
                public long ChatId = 0;
                public long GroupId = 0;
                public JToken Row;
                public EventObject(JToken response)
                {
                    Row = response;
                    if (Convert.ToInt32(response[0]) == 4)
                    {
                        Type = VKEventType.MessageNew;
                        MessageId = response[1].ToObject<long>();
                        PeerId = response[3].ToObject<long>();
                        Time = response[4].ToObject<long>();
                        Message = response[5].ToString();
                        Extra = response[6];
                        Attachments = response[7];
                        if(PeerId < 0)
                        {
                            FromGroup = true;
                            GroupId = -PeerId;
                        }
                        else if(PeerId > 2000000000)
                        {
                            FromChat = true;
                            ChatId = PeerId - 2000000000;

                            if(Extra != null)
                            {
                                UserId = Extra["from"].ToObject<long>();
                            }
                        }
                        else
                        {
                            FromUser = true;
                            UserId = PeerId;
                        }
                    }
                }
            }
        }
        public enum VKEventType
        {
            Other,
            MessageNew
        }
        public class Error
        {
            public class RequestException : Exception { public RequestException() : base("Cannot send request.") { } }
            public class ApiException : Exception
            {
                public ErrorCode Code;
                public ApiException(string message, ErrorCode code) : base(message)
                {
                    this.Code = code;
                }
            }
            public enum ErrorCode
            {
                Other,
                AccessToken
            }
        }
    }
}
