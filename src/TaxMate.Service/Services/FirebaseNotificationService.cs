using Microsoft.Extensions.Configuration;
using TaxMate.Service.Interfaces;

namespace TaxMate.Service.Services;

using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

public class FirebaseNotificationService
    : IFirebaseNotificationService
{
    public FirebaseNotificationService(
        IConfiguration configuration)
    {
        if (FirebaseApp.DefaultInstance == null)
        {
            FirebaseApp.Create(
                new AppOptions
                {
                    Credential =
                        GoogleCredential
                            .FromFile(
                                configuration["Firebase:ServiceAccountPath"])
                });
        }
    }

    public async Task SendAsync(
        string token,
        string title,
        string body)
    {
        var message = new Message
        {
            Token = token,

            Notification = new Notification
            {
                Title = title,
                Body = body
            }
        };

        await FirebaseMessaging.DefaultInstance
            .SendAsync(message);
    }
}