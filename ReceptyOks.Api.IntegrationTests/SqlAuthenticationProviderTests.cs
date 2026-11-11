using Microsoft.Data.SqlClient;

namespace ReceptyOks.Api.IntegrationTests;

// Production connects to Azure SQL with Authentication=Active Directory Managed Identity.
// Since Microsoft.Data.SqlClient 7 the Entra ID providers ship in Microsoft.Data.SqlClient.Extensions.Azure;
// without it the API crashes on startup in Azure Container Apps.
[TestFixture]
public class SqlAuthenticationProviderTests
{
    [TestCase(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity)]
    [TestCase(SqlAuthenticationMethod.ActiveDirectoryDefault)]
    public void GetProvider_EntraIdMethod_ReturnsRegisteredProvider(SqlAuthenticationMethod method)
    {
        var provider = SqlAuthenticationProvider.GetProvider(method);

        Assert.That(provider, Is.Not.Null);
    }
}
