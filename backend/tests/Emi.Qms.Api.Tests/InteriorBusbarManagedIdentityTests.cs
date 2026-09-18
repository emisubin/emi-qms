using Azure.Core;
using Azure.Identity;
using Emi.Qms.Api.InteriorBusbar;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Headers;
using Xunit;
namespace Emi.Qms.Api.Tests;
public sealed class InteriorBusbarManagedIdentityTests
{
    private const string ClientId = "11111111-2222-3333-4444-555555555555";
    private static Dictionary<string,string?> Values() => new()
    {
        ["InteriorBusbar:Publication:Enabled"]="true",
        ["Frontend:Origin"]="https://pms.example.test",
        ["InteriorBusbar:Publication:AuthenticationMode"]="ManagedIdentity",
        ["InteriorBusbar:Publication:ManagedIdentityClientId"]=ClientId,
        ["InteriorBusbar:Publication:PublicBaseUrl"]="https://products.z1.web.core.windows.net/",
        ["InteriorBusbar:Publication:BlobEndpoint"]="https://products.blob.core.windows.net/",
    };
    private static InteriorBusbarPublicationOptions Load(Dictionary<string,string?> values) => InteriorBusbarPublicationOptions.Load(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    [Fact]
    public void ManagedIdentityNeedsNoSasAndReviewSafeStillDisablesPublishing()
    {
        var values=Values();var options=Load(values);
        Assert.True(options.Enabled);Assert.Equal(ClientId,options.ManagedIdentityClientId);Assert.Empty(options.SasToken);
        values["ReviewSafe:Enabled"]="true";Assert.False(Load(values).Enabled);
    }
    [Theory]
    [InlineData("AuthenticationMode","DefaultAzureCredential")]
    [InlineData("ManagedIdentityClientId","")]
    [InlineData("ManagedIdentityClientId","invalid")]
    [InlineData("SasToken","must-not-use-two-identities")]
    public void InvalidOrAmbiguousIdentityFailsClosed(string key,string value)
    {
        var values=Values();values["InteriorBusbar:Publication:"+key]=value;
        Assert.Throws<InvalidOperationException>(()=>Load(values));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BearerUsesStorageScopeAndConditionalWritesWithoutQuerySecrets(bool exists)
    {
        var credential=new RecordingCredential();var handler=new RecordingHandler(exists);
        using var sink=new AzureInteriorBusbarPublicationSink(Load(Values()),handler,credential);
        await sink.PublishAsync(new string('a',64),[1,2,3],TestContext.Current.CancellationToken);
        Assert.Equal(2,handler.Calls);Assert.Equal(2,credential.Calls);
    }
    [Fact]
    public async Task TokenFailureDoesNotFallBackOrSendStorageRequest()
    {
        var handler=new RecordingHandler(false);
        using var sink=new AzureInteriorBusbarPublicationSink(Load(Values()),handler,new RecordingCredential(true));
        await Assert.ThrowsAsync<CredentialUnavailableException>(()=>sink.PublishAsync(new string('a',64),[1],TestContext.Current.CancellationToken));
        Assert.Equal(0,handler.Calls);
    }
    [Fact]
    public async Task DisabledSinkDoesNotRequestTokenOrAccessStorage()
    {
        var handler=new RecordingHandler(false);var credential=new RecordingCredential();
        using var sink=new AzureInteriorBusbarPublicationSink(Load(Values()) with {Enabled=false},handler,credential);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>sink.PublishAsync(new string('a',64),[1],TestContext.Current.CancellationToken));
        Assert.Equal(0,credential.Calls);Assert.Equal(0,handler.Calls);
    }
    private sealed class RecordingCredential(bool fail=false):TokenCredential
    {
        public int Calls;
        public override AccessToken GetToken(TokenRequestContext context,CancellationToken token)=>throw new NotSupportedException();
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context,CancellationToken token)
        {
            Calls++;Assert.Equal("https://storage.azure.com/.default",Assert.Single(context.Scopes));
            if(fail)throw new CredentialUnavailableException("synthetic missing identity");
            return ValueTask.FromResult(new AccessToken("synthetic-token-"+Calls,DateTimeOffset.UtcNow.AddMinutes(10)));
        }
    }
    private sealed class RecordingHandler(bool exists):HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Calls++;Assert.Equal("products.blob.core.windows.net",request.RequestUri!.Host);Assert.Empty(request.RequestUri.Query);
            Assert.Equal("Bearer",request.Headers.Authorization?.Scheme);Assert.Equal("synthetic-token-"+Calls,request.Headers.Authorization?.Parameter);
            Assert.True(request.Headers.Contains("x-ms-date"));Assert.Equal("2023-11-03",Assert.Single(request.Headers.GetValues("x-ms-version")));
            if(request.Method==HttpMethod.Head){var response=new HttpResponseMessage(exists?HttpStatusCode.OK:HttpStatusCode.NotFound);if(exists)response.Headers.ETag=new EntityTagHeaderValue("\"v1\"");return Task.FromResult(response);}
            Assert.Equal(HttpMethod.Put,request.Method);
            if(exists)Assert.Equal("\"v1\"",Assert.Single(request.Headers.IfMatch).Tag);else Assert.Equal("*",Assert.Single(request.Headers.IfNoneMatch).Tag);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
        }
    }
}
