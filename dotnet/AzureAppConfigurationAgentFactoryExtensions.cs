// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Primitives;

namespace Microsoft.SemanticKernel.Agents.AppConfiguration;

/// <summary>
/// Extension methods for <see cref="AgentFactory"/> to create agents from Azure App Configuration.
/// </summary>
public static class AzureAppConfigurationAgentFactoryExtensions
{
    private static IConfigurationRefresher? s_refresher;

    private static IConfiguration? configuration;

    private static IChangeToken? s_changeToken;
    /// <summary>
    /// Create an agent from Azure App Configuration.
    /// </summary>
    /// <param name="kernelAgentFactory">Kernel agent factory which will be used to create the agent.</param>
    /// <param name="connectionString">Connection string for Azure App Configuration.</param>
    /// <param name="key">Key for the configuration setting.</param>
    /// <param name="label">Label for the configuration setting.</param>
    /// <param name="options">Optional <see cref="AgentCreationOptions"/> instance.</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    public static async Task<Agent?> CreateAgentFromAzureAppConfigurationAsync(
        this AgentFactory kernelAgentFactory,
        string connectionString,
        string key,
        string? label = null,
        AgentCreationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var builder = new ConfigurationBuilder();

        builder.AddAzureAppConfiguration(options =>
        {
            options.Connect(connectionString)
                   .Select(key, label)
                   .ConfigureRefresh(refreshOptions =>
                   {
                       refreshOptions.RegisterAll();
                   });

            s_refresher = options.GetRefresher();
        });

        configuration = builder.Build();

        var agentDefinition = new AgentDefinition();

        configuration.Bind(agentDefinition);

        s_changeToken = configuration.GetReloadToken();

        return await kernelAgentFactory.CreateAsync(
            options?.Kernel ?? new Kernel(),
            agentDefinition,
            options,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<Agent?> RefreshAgentAsync(
        this AgentFactory kernelAgentFactory,
        AgentCreationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (s_refresher is null)
        {
            throw new InvalidOperationException("Configuration refresher is not initialized. Ensure you have called CreateAgentFromAzureAppConfigurationAsync first.");
        }

        if (configuration is null)
        {
            throw new InvalidOperationException("Configuration is not initialized. Ensure you have called CreateAgentFromAzureAppConfigurationAsync first.");
        }

        if (s_changeToken is null)
        {
            throw new InvalidOperationException("Change token is not initialized. Ensure you have called CreateAgentFromAzureAppConfigurationAsync first.");
        }

        await s_refresher.TryRefreshAsync(cancellationToken);

        if (s_changeToken.HasChanged)
        {
            s_changeToken = configuration.GetReloadToken();

            var agentDefinition = new AgentDefinition();

            if (configuration is null)
            {
                throw new InvalidOperationException("Configuration is not initialized. Ensure you have called CreateAgentFromAzureAppConfigurationAsync first.");
            }

            configuration.Bind(agentDefinition);

            return await kernelAgentFactory.CreateAsync(
                options?.Kernel ?? new Kernel(),
                agentDefinition,
                options,
                cancellationToken).ConfigureAwait(false);
        }

        return null;
    }
}
