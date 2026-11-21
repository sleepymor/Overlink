using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace OverlinkCompetitiveProbe;

public sealed class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config) =>
        config.Dependencies.AddSingleton<IGameApiClient>(GameApiClient.Create());
}

// Isolated diagnostic, not a match server. Do not deploy outside testing.
public sealed class Probe
{
    private const string TestingEnvironment = "ff807509-790a-4ab0-90aa-0b2a9aac70dc";
    private const string Project = "467bcece-28d8-4091-9b3c-8138c7a02dca";
    private const string Key = "probe";

    [CloudCodeFunction("ProbePrivateWriteLock")]
    public async Task<ProbeResult> ProbePrivateWriteLock(IExecutionContext context, IGameApiClient api)
    {
        if (context.ProjectId != Project || context.EnvironmentId != TestingEnvironment ||
            string.IsNullOrEmpty(context.PlayerId) || string.IsNullOrEmpty(context.ServiceToken))
            throw new InvalidOperationException("Probe requires an authenticated player in the Overlink testing environment.");

        string id = "competitive_probe_" + Guid.NewGuid().ToString("N");
        var cloud = api.CloudSaveData;
        await cloud.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, id,
            new SetItemBody(Key, "initial"));
        var loaded = await cloud.GetPrivateCustomItemsAsync(context, context.ServiceToken, context.ProjectId, id,
            new List<string> { Key });
        var item = loaded.Data.Results.Single(x => x.Key == Key);
        if (string.IsNullOrEmpty(item.WriteLock)) throw new InvalidOperationException("Cloud Save returned no write lock.");
        string staleLock = item.WriteLock;
        await cloud.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, id,
            new SetItemBody(Key, "committed", staleLock));

        bool conflict = false;
        try
        {
            await cloud.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, id,
                new SetItemBody(Key, "stale", staleLock));
        }
        catch (ApiException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
        {
            conflict = true;
        }

        var final = await cloud.GetPrivateCustomItemsAsync(context, context.ServiceToken, context.ProjectId, id,
            new List<string> { Key });
        string value = final.Data.Results.Single(x => x.Key == Key).Value?.ToString() ?? "";
        return new ProbeResult
        {
            CustomId = id,
            StaleWriteRejected = conflict,
            CommittedValuePreserved = value == "committed"
        };
    }
}

public sealed class ProbeResult
{
    public string CustomId { get; set; } = "";
    public bool StaleWriteRejected { get; set; }
    public bool CommittedValuePreserved { get; set; }
}
