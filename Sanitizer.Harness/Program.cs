using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Sanitizer.BL;
using System;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace Sanitizer.Harness
{
    class Program
    {
        //============= Config [Edit these with your settings] =====================
        internal const string azureDevOpsOrganizationUrl = "https://microsoftit.visualstudio.com/"; //change to the URL of your Azure DevOps account; NOTE: This must use HTTPS
        internal const string clientId = "872cd9fa-d31f-45e0-9eab-6e460a02d1f1";          //change to your app registration's Application ID, unless you are an MSA backed account
        internal const string replyUri = "urn:ietf:wg:oauth:2.0:oob";                     //change to your app registration's reply URI, unless you are an MSA backed account
        //==========================================================================

        internal const string azureDevOpsResourceId = "499b84ac-1321-427f-aa17-267ca6975798"; //Constant value to target Azure DevOps. Do not change  
        static VSOHelper helper = new VSOHelper(azureDevOpsOrganizationUrl, clientId, replyUri);

        public static void Main(string[] args)
        {
            Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationContext ctx = GetAuthenticationContext(null);
            AuthenticationResult result = null;
            IPlatformParameters promptBehavior = new PlatformParameters(PromptBehavior.Auto);

            try
            {
                //PromptBehavior.RefreshSession will enforce an authn prompt every time. NOTE: Auto will take your windows login state if possible
                result = ctx.AcquireTokenAsync(azureDevOpsResourceId, clientId, new Uri(replyUri), promptBehavior).Result;
                Console.WriteLine("Token expires on: " + result.ExpiresOn);

                var bearerAuthHeader = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                helper.ListRepos(bearerAuthHeader, "HR-TAL");

                //Collect Branches for Repo
                int counter = 0;
                //helper.RepoList.ForEach(r =>
                //{
                //    Console.WriteLine(String.Format("Processing repo {0} > {1}", ++counter, r.name));
                //    helper.ListBranchesForRepo(r);
                //    helper.ListBuildsForRepoBranch(r, "refs/heads/master");
                //});
                Console.WriteLine("Fetching all releases as per specified filters...");
                helper.ListReleases(bearerAuthHeader, "HR-");
                Console.WriteLine("--------------------------------------------------");
                DumpCSV();
                Console.WriteLine("-- DONE --");
                Console.ReadLine();
            }
            catch (UnauthorizedAccessException)
            {
                // If the token has expired, prompt the user with a login prompt
                result = ctx.AcquireTokenAsync(azureDevOpsResourceId, clientId, new Uri(replyUri), promptBehavior).Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}: {1}", ex.GetType(), ex.Message);
            }
            finally
            {

            }
        }

        private static void DumpCSV()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("RelID,Release Name,ModifiedOn,URL");
            helper.ReleasesList.ForEach(r => {
                sb.Append(r.id);
                sb.Append(",");
                sb.Append(r.name);
                sb.Append(",");
                sb.Append(r.modifiedOn);
                sb.Append(",");
                sb.Append(r.url);
                sb.AppendLine();
            });
            File.WriteAllText("ReleasesList.csv", sb.ToString());
        }

        private static Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationContext GetAuthenticationContext(string tenant)
        {
            Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationContext ctx = null;
            if (tenant != null)
                ctx = new Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationContext("https://login.microsoftonline.com/" + tenant);
            else
            {
                ctx = new Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationContext("https://login.windows.net/common");
                if (ctx.TokenCache.Count > 0)
                {
                    string homeTenant = ctx.TokenCache.ReadItems().First().TenantId;
                    ctx = new Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationContext("https://login.microsoftonline.com/" + homeTenant);
                }
            }

            return ctx;
        }


    }
}
