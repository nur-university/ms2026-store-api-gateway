using Consul;
using Joseco.Secrets.Contrats;
using Joseco.Secrets.HashicorpVault;
using Nur.Store2026.Observability.Config;
using Nur.Store2026.Security.Config;
using System;

namespace ApiGateway.WebApp.Extensions;

public static class SecretExtensions
{
    public static IServiceCollection AddSecrets(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        bool useSecretManager = configuration.GetValue<bool>("UseSecretManager", false);
        if (environment.IsDevelopment() && !useSecretManager)
        {
            string JwtOptionsSecretName = "JwtOptions";
            string JeagerSettingsSecretName = "JaegerSettings";

            configuration
                .LoadAndRegister<JwtOptions>(services, JwtOptionsSecretName)
                .LoadAndRegister<JeagerSettings>(services, JeagerSettingsSecretName);

            return services;
        }

        string? vaultUrl = Environment.GetEnvironmentVariable("VAULT_URL");
        string? vaultToken = Environment.GetEnvironmentVariable("VAULT_TOKEN");

        if (string.IsNullOrEmpty(vaultUrl) || string.IsNullOrEmpty(vaultToken))
        {
            throw new InvalidOperationException("Vault URL or Token is not set in environment variables.");
        }

        var settings = new VaultSettings()
        {
            VaultUrl = vaultUrl,
            VaultToken = vaultToken
        };

        services.AddHashicorpVault(settings)
            .LoadSecretsFromVault();

        return services;
    }
    private static void LoadSecretsFromVault(this IServiceCollection services)
    {
        string JwtOptionsSecretName = "JwtOptions";
        string VaultMountPoint = "secrets";

        using var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        using var scope = scopeFactory.CreateScope();
        var secretManager = scope.ServiceProvider.GetRequiredService<ISecretManager>();

        Task[] tasks = [
                LoadAndRegister<JwtOptions>(secretManager, services, JwtOptionsSecretName, VaultMountPoint),
            ];

        Task.WaitAll(tasks);
    }

    private static async Task LoadAndRegister<T>(ISecretManager secretManager, IServiceCollection services,
        string secretName, string mountPoint) where T : class, new()
    {
        T secret = await secretManager.Get<T>(secretName, mountPoint);
        services.AddSingleton<T>(secret);
    }



    private static IConfiguration LoadAndRegister<T>(this IConfiguration configuration, IServiceCollection services,
        string secretName) where T : class, new()
    {
        T secret = Activator.CreateInstance<T>();
        configuration.Bind(secretName, secret);
        services.AddSingleton<T>(secret);
        return configuration;
    }
}
