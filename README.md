# TelegramBotCoreFramework

## Overview

TelegramBotCoreFramework is a versatile Telegram bot framework for easy deployment and customization, ideal for managing whole networks of Telegram channels by many of people.
This project allows developers to quickly deploy a generic Telegram bot and add new functionalities as needed.

## Client-Side Features


1. **Single Bot for Multiple Channels**: Manage a network of channels using one bot, which can also serve as a welcoming bot.
2. **Detailed Post Planning Reports**: Offers reports for planning, placement, and deletion of posts, improving service quality and customer satisfaction.
3. **Automated Creative Generation**: Automated creative generation based on current statistics, saving time in manual collection.
4. **Efficient Ad Purchasing**: Features automatic updating of links in creatives across different channels.
5. **In-depth Analytics**: Detailed data on traffic, user interaction, and post effectiveness.
6. **Welcoming Bot**: Utilized for traffic monetization, redirecting traffic or conducting targeted mailouts.
7. **Data Security and Stability**: Each client has a separate database for confidentiality and data security, with infrastructure support from Google Cloud.
8. **Interactive Subscriber Engagement**: Includes a feedback bot for anonymous communication with subscribers.

## Developer Perspective

For the developer perspective, the TelegramBotCoreFramework offers several appealing features:
1. **Easy Deployment and Customization**: Designed for straightforward deployment and scalability, allowing for easy adaptation to different client needs.
2. **Technology Stack**: Built on a robust technology stack, including ASP.NET, Docker, and Google Cloud, ensuring reliability and efficiency.
3. **Security**: Strong emphasis on data security, with each client having a separate database for confidentiality.
4. **Scalability**: Ready for scaling, supported by Google Cloud's infrastructure.
5. **Customizability**: Framework's structure allows developers to easily add new functionalities or modify existing ones, tailoring to specific client requirements.

This framework is ideal for developers seeking a scalable, secure, and customizable base for building Telegram bots.

---

## Installation & Deployment

### Requirements

- Any OS that supports Docker, Nginx and ASP.NET (Ubuntu on GCE is used as an example).

### Installation Guide

1. **Setting Up the Environment**
   - Set up a VM on Google Cloud Platform (GCP). [GCP VM documentation](https://cloud.google.com/compute/docs/instances). (project can run on other environments as well, but is designed for GCP and next commands tested only on GCP)
   - Install Docker: Follow the [official Docker installation guide](https://docs.docker.com/get-docker/).
   - Install Nginx: Refer to the [Nginx installation manual](http://nginx.org/en/docs/install.html).
   
2. **Nginx Configuration**
   - Configure Nginx for your project. Example web host configuration:
   ``` nginx config
   server {
       listen 8443 ssl;
       server_name [HOST];
   
       ssl_certificate /etc/letsencrypt/live/[HOST]/fullchain.pem;
       ssl_certificate_key /etc/letsencrypt/live/[HOST]/privkey.pem;
   
       location /[CLIENT_NAME]/ {
           rewrite ^/[CLIENT_NAME](/.*)$ $1 break;
           proxy_pass http://localhost:12100;
           proxy_set_header Host $host;
       }
   
       location /[CLIENT2_NAME]/ {
           rewrite ^/[CLIENT2_NAME](/.*)$ $1 break;
           proxy_pass http://localhost:12101;
           proxy_set_header Host $host;
       }
   }
   ```

3. **Docker Image**
   - Build and push the Docker image to Google Artifact Registry.
   ``` shell
   cd [Repository Root]/TelegramBotCoreFramework
   docker build -t [Registry URL]/[Project Name]/[Image Name]:latest -f ./core/Dockerfile .
   docker push [Registry URL]/[Project Name]/[Image Name]:latest
   ```
   
4. **Running on VM**
   - SSH into GCE VM and deploy the Docker container.
   ```shell
   gcloud compute ssh [VM Instance] --project [Project Name] --zone [Zone]
   ```
   - Run commands to pull and run the Docker container on the VM.
   ```shell
   # SSH and stop previous container (if any) 
   sudo docker stop [Container Name] 
   sudo docker rm [Container Name]
   
   # Authenticate and pull the Docker image 
   gcloud auth print-access-token | sudo docker login -u oauth2accesstoken --password-stdin [Registry URL] 
   
   # Run the Docker container 
   sudo docker pull [Registry URL]/[Project Name]/[Image Name]:latest
   # variables here may be:
   # -e ClientName="[Client Name]" 
   # -e TelegramBotToken="[Bot Token]" 
   # -e SiteURL="https://[Domain URL]:[External port]/[Client name from nginx]" 
   # -e GoogleProjectId="[Project Id]" 
   # -e BigQueryDatasetId="[BigQuery Dataset Id]"
   sudo docker run -d -p [Port inside docker]:[Port in nginx] -e [Environment Variables] --name=[Container Name] [Registry URL]/[Project Name]/[Image Name]:latest
   ```
   
5. **Retrieving Logs**
   - Command to get logs from the remote container on the VM:
   ``` shell
   gcloud compute ssh [VM Instance] --project [Project Name] --zone [Zone] --command 'sudo docker logs [Container Name]'
   ```
   
6. **Stopping Deployments**
   - Command to stop and remove the container on the VM:
   ``` shell
   gcloud compute ssh [VM Instance] --project [Project Name] --zone [Zone] --command 'sudo docker stop [Container Name] || true && sudo docker rm [Container Name] || true'
   ```


---

## Adding a New Client

### Pre-Requirements

- A Telegram bot token obtained from [@BotFather](https://t.me/botfather).
- The nickname of the bot for verification purposes.
- Access to GCP services like BigQuery, Firestore, and Pub/Sub.
- Take in mind that some names are hardcoded for now, so you should use only exact names when other not specified. Review filed `Dockerfile` and `Env.cs` and `C.cs` for more details about predefined variables.

### Steps to Add a New Client

1. **Google Cloud Console Setup**
   - Visit the [Google Cloud Console](https://console.cloud.google.com/).
   - Create a new project and select it.
   - Grant access to the GCE service account `[Service Account Email]` with roles `Editor`, `BigQuery Data Editor`, `BugQuery Job User`, `Firestore Service Agent`. If you don't use VM on GCE - you can create new service account and grant it access to necessary services.
   
2. **Firestore Configuration**
   - Go to Firestore in the Google Cloud Console and create a new Native DB in your preferred zone.
   - Create necessary indexes for your collections. _Note: May be good idea to review all bot functionality and create indexes when error in runtime will appears._  

3. **BigQuery Setup**
   - In BigQuery, create a new dataset, e.g., `channels_analytics`, in the preferred zone.
   - Use the `[Repo path]/TelegramBotCoreFramework/Analytics/bq_schemas.sql` file from the source repository as source to create new tables.
   
4. **Pub/Sub Topic Creation**
   - Create necessary Pub/Sub topics: `llbots-tg-ingestion` and `llbots-tg-analytics-schedule` with default subscriptions.
   
5. **Nginx Configuration**
   - Update the Nginx configuration on the VM.
   - Use the template file to update `/etc/nginx/sites-available/yourproject`.
   - Reload Nginx to apply changes.
   
6. **Docker Image Deployment**
   - Deploy the Docker image for with related environment variables for the new client.
   - Run a GET request to set up the bot's webhook: `https://[Domain URL]:[External port]/[Client name from nginx]/LlBotsSetupBotWebhook`

7. **Cloud Scheduler**
   - Set up Cloud Scheduler triggers for analytics collection. Usually, you will have to setup next triggers:
      
| Name | Region | Schedule | Timezone | Target | Payload |
| --- | --- | --- | --- | --- | --- |
| `Trigger_collect_channel_admin_log` | europe-central2 | `*/20 * * * *` | UTC | Pub/Sub, analytics-schedule | `collect_channel_admin_log` |
| `Trigger_collect_channel_participants` | europe-central2 | `0 3 * * *` | UTC | Pub/Sub, analytics-schedule | `collect_channel_participants` |
| `Trigger_collect_channels_subscribers_count` | europe-central2 | `*/20 * * * *` | UTC | Pub/Sub, analytics-schedule | `collect_channels_subscribers_count` |
| `Trigger_collect_last_messages_views` | europe-central2 | `*/20 * * * *` | UTC | Pub/Sub, analytics-schedule | `collect_last_messages_views` |

### Additional Steps

- Verify the bot's functionality in Telegram.
- Add client-specific users to the Firestore database.
- Check all bot functionality and create necessary indexes in Firestore.

---

## Project Structure

The TelegramBotCoreFramework solution comprises several projects, each encapsulating specific functionalities:

1. **Analytics**: Contains the logic for data analytics and reporting.
2. **CommunicationChat**: Manages the interaction and communication with the Telegram API.
3. **Helpers**: Provides utility functions and helper classes.
4. **TG.UpdatesProcessing**: Handles the processing of updates from Telegram.
5. **TG.Webhooks.Processing**: Manages webhook-based interactions.
6. **core**: The entry point project which is built and run. Other projects are DLL libraries injected into the core using .NET DI.

### Extension Example

The framework is designed to easily incorporate specific client logic. For example, in the `Program.js` file of the main project, various services are added based on the client environment:
```c#
builder.Services.AddAnalyticsServices();
builder.Services.AddHelpersServices();
// ... other services
switch (Env.ClientName)
{
    case "ExampleClientName":
        builder.Services.AddClientSpecificBindings();
        break;
}
```

Each client-specific project can have its entry point for DI, as shown in the example with `AddClientSpecificBindings`. This method enables developers to tailor the bot to the unique needs of each client.

### Custom Command Implementation in `SpecificToDevEnv`

The `SpecificToDevEnv` project serves as an example of how to implement custom commands for different client environments. For instance:

1. **DevSettingsBotCommand**: This command offers developer-specific settings, accessible only to the bot owner. It inherits from `BotCommandBase` and overrides necessary properties and methods.

2. **PrintHelloWorldBotCommand**: A simple command that responds with "Hello World" when invoked. It also demonstrates basic command structure and interaction with the Telegram API.


Example implementation:

```c#
// Entry point for DI to add client-specific bindings
public static class TgUpdatesProcessingExtensions
{
    public static IServiceCollection AddDevEnvSpecificBindings(this IServiceCollection services)
    {
        services.AddTransient<IBotCommand, DevSettingsBotCommand>();
        services.AddTransient<IBotCommand, PrintHelloWorldBotCommand>();
        return services;
    }
}

// Example of a custom "parent" command
public class DevSettingsBotCommand : BotCommandBase
{
    public override string CommandName => "⚙ Тестові налаштування";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(MainMenuBotCommand);

    public DevSettingsBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
    }
}

// Example of a custom "child" command
public class PrintHelloWorldBotCommand : BotCommandBase
{
    public override string CommandName => "👋 Привіт світ";
    public override CommandsAccessLevel AccessLevel => CommandsAccessLevel.Owner;
    public override Type? ParentCommandType => typeof(DevSettingsBotCommand);

    public PrintHelloWorldBotCommand(TelegramBotClient botClient, IBotCommandsFactory botCommandsFactory,
        AdminsController adminsController, AdminUsers adminUsers) 
        : base(botClient, botCommandsFactory, adminsController, adminUsers)
    {
    }

    public override async Task<CommandResult> ProcessMessage(Update update, string[]? args,
        string? reroutedForPath = null)
    {
        await SendTextMessageWithDefaultCommandButton(update, "Hello world!");
        return CommandResult.Ok;
    }
}

```

This project illustrates how you can extend the bot's functionality by adding new commands tailored to specific client needs or development environments.

---

### Troubleshooting

- **Common Issues and Solutions**: This section will be populated with frequently encountered issues and their solutions. (To be filled based on user feedback and common challenges faced during the deployment and usage of the bot.)

### Contribution Guidelines

- **How to Contribute**: We welcome contributions! Please submit pull requests with clear descriptions of changes and the benefits they bring to the project.
- **Code of Conduct**: All contributors are expected to adhere to the project's code of conduct, fostering a respectful and collaborative community.
- **Issue Reporting**: For reporting issues, use the GitHub issues section, providing detailed information for reproducibility.

### Contact

- **Support and Queries**: For any support or questions, feel free to contact us through the GitHub project's issues section or the provided contact details on the repository page.