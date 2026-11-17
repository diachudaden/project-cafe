namespace ChonRieng.Api.Services.Email;

public static class EmailRegistration
{
    public static IServiceCollection AddEmailDelivery(this IServiceCollection services)
    {
        services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<AppOptions>()
            .BindConfiguration(AppOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(o => AppOptions.IsValidBaseUrl(o.PublicBaseUrl), "App__PublicBaseUrl must be an absolute http/https URL without query or fragment.")
            .ValidateOnStart();
        services.AddOptions<PasswordResetOptions>()
            .BindConfiguration(PasswordResetOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
        services.AddHostedService<EmailDispatchWorker>();
        return services;
    }
}
