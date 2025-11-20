using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;


namespace Taxxor.Project
{
    public abstract partial class ProjectLogic : Framework
    {
        public static class EmailService
        {
            private class Config
            {
                public string TenantId { get; set; }
                public string ClientId { get; set; }
                public string ClientSecret { get; set; }
            }

            private static GraphServiceClient GetGraphServiceClient()
            {
                var config = LoadConfig();

                var clientSecretCredential = new ClientSecretCredential(config.TenantId, config.ClientId, config.ClientSecret);
                return new GraphServiceClient(clientSecretCredential);
            }

            private static Config LoadConfig()
            {
                var configFilePath = $"{applicationRootPathOs}/secrets/keys/mail/config.json";
                if (!File.Exists(configFilePath))
                {
                    throw new FileNotFoundException($"Mail configuration file not found at {configFilePath}");
                }
                var json = File.ReadAllText(configFilePath);
                return JsonSerializer.Deserialize<Config>(json);
            }


            public static async Task<TaxxorReturnMessage> SendGraphEmailAsync(string subject, string body, string to)
            {
                try
                {
                    var client = GetGraphServiceClient();

                    var message = new Message
                    {
                        Subject = subject,
                        Body = new ItemBody
                        {
                            ContentType = BodyType.Html,
                            Content = body
                        },
                        ToRecipients = new List<Recipient>
                    {
                        new() {
                            EmailAddress = new Microsoft.Graph.Models.EmailAddress
                            {
                                Address = to
                            }
                        }
                    }
                    };

                    Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody mailbody = new()
                    {
                        Message = message,
                        SaveToSentItems = false  // or true, as you want
                    };

                    try
                    {
                        await client.Users["support@taxxor.com"]
                        .SendMail
                        .PostAsync(mailbody);
                        return new TaxxorReturnMessage(true, "Successfully sent email with Microsoft Graph API");
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError(ex, "Failed to send email with Microsoft Graph API");
                        return new TaxxorReturnMessage(false, "Failed to send email with Microsoft Graph API", ex.ToString());
                    }
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, "Failed setting up mail client for Microsoft Graph API");
                    return new TaxxorReturnMessage(false, "Failed setting up mail client for Microsoft Graph API", ex.ToString());
                }


            }

        }
    }
}