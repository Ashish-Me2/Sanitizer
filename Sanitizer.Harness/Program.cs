using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Mvc.Filters;

using Sanitizer.BL;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Web.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Sanitizer.Harness.Models;

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
        private static List<Repository> RepositoriesList = new List<Repository>();
        private static HttpClient globalWebClient;

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
                //ListProjects(bearerAuthHeader);
                ListRepos(bearerAuthHeader, "HR");

                //Collect Branches for Repo
                RepositoriesList.ForEach(r =>
                {
                    Console.WriteLine("Processing repo = " + r.name);
                    ListBranchesForRepo(bearerAuthHeader, r);
                    ListBuildsForRepoBranch(bearerAuthHeader, r, "refs/heads/master");
                });

                //Collect Builds for a Branch
                //RepositoriesList.ForEach(r =>
                //{
                //    Console.WriteLine("Processing builds for repo = " + r.name);

                //});

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
                globalWebClient.Dispose();
            }
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

        private static void ListRepos(AuthenticationHeaderValue authHeader, string repoNameStartFilter)
        {
            if (globalWebClient == null)
            {
                globalWebClient = new HttpClient();
                globalWebClient.BaseAddress = new Uri(azureDevOpsOrganizationUrl);
                globalWebClient.DefaultRequestHeaders.Accept.Clear();
                globalWebClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                globalWebClient.DefaultRequestHeaders.Add("User-Agent", "ManagedClientConsoleAppSample");
                globalWebClient.DefaultRequestHeaders.Add("X-TFS-FedAuthRedirect", "Suppress");
                globalWebClient.DefaultRequestHeaders.Authorization = authHeader;
            }

            // connect to the REST endpoint            
            HttpResponseMessage response = globalWebClient.GetAsync("OneITVso/_apis/git/repositories?api-version=5.1").Result;

            // check to see if we have a succesfull respond
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;
                JObject repoList = JObject.Parse(result);
                List<JToken> repoData = ((JArray)repoList["value"]).ToList();
                repoData.ForEach(r =>
                {
                    Repository _r = new Repository();
                    bool recordFlag = false;
                    List<JToken> repoAttribs = (r.Children()).ToList();
                    repoAttribs.ForEach(p =>
                    {
                        if (_r.name == null)
                            _r.name = (((JProperty)p).Name == "name") ? ((JProperty)p).Value.ToString() : null;
                        if (_r.id == null)
                            _r.id = (((JProperty)p).Name == "id") ? ((JProperty)p).Value.ToString() : null;
                        if (_r.url == null)
                            _r.url = (((JProperty)p).Name == "url") ? ((JProperty)p).Value.ToString() : null;
                    });
                    if (!String.IsNullOrEmpty(repoNameStartFilter))
                    {
                        if (_r.name.ToUpper().StartsWith(repoNameStartFilter.ToUpper()))
                        {
                            recordFlag = true;
                        }
                    }
                    else
                    {
                        recordFlag = true;
                    }

                    if (recordFlag)
                    {
                        RepositoriesList.Add(_r);
                    }
                });
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new UnauthorizedAccessException();
            }
            else
            {
                Console.WriteLine("{0}:{1}", response.StatusCode, response.ReasonPhrase);
            }
        }

        private static void ListBuildsForRepoBranch(AuthenticationHeaderValue authHeader, Repository Repository, string BranchName)
        {
            // connect to the REST endpoint            
            HttpResponseMessage response = globalWebClient.GetAsync("OneITVso/_apis/build/builds?branchName=" + BranchName + "&repositoryType=TfsGit&repositoryId=" + Repository.id + "&api-version=5.1").Result;

            // check to see if we have a succesfull respond
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;
                JObject buildsList = JObject.Parse(result);
                List<Build> branchBuilds = new List<Build>();
                List<JToken> buildData = ((JArray)buildsList["value"]).ToList();
                buildData.ForEach(r =>
                {
                    Build _b = new Build();
                    List<JToken> buildAttribs = (r.Children()).ToList();
                    buildAttribs.ForEach(p =>
                    {
                        if (_b.id == null)
                            _b.id = (((JProperty)p).Name == "id") ? ((JProperty)p).Value.ToString() : null;
                        if (_b.url == null)
                            _b.url = (((JProperty)p).Name == "url") ? ((JProperty)p).Value.ToString() : null;
                        if (_b.number == null)
                            _b.number = (((JProperty)p).Name == "buildNumber") ? ((JProperty)p).Value.ToString() : null;

                        if (((JProperty)p).Name == "definition")
                        {
                            JToken buildDefinitionAttribs = ((JProperty)p).Value;
                            buildDefinitionAttribs.Children().ToList().ForEach(c => {
                                if (_b.name == null)
                                    _b.name = (((JProperty)c).Name == "name") ? ((JProperty)c).Value.ToString() : null;
                            });
                        }
                    });
                    branchBuilds.Add(_b);
                });
                Repository.branches.Find(f=>(f.name==BranchName)).builds = branchBuilds;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new UnauthorizedAccessException();
            }
            else
            {
                Console.WriteLine("{0}:{1}", response.StatusCode, response.ReasonPhrase);
            }
        }

        private static void ListBranchesForRepo(AuthenticationHeaderValue authHeader, Repository Repository)
        {
            // connect to the REST endpoint            
            HttpResponseMessage response = globalWebClient.GetAsync("OneITVso/_apis/git/repositories/" + Repository.id + "/refs?api-version=5.1").Result;

            // check to see if we have a succesfull respond
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;
                JObject branchList = JObject.Parse(result);
                List<Branch> repoBranches = new List<Branch>();
                List<JToken> branchData = ((JArray)branchList["value"]).ToList();
                branchData.ForEach(r =>
                {
                    Branch _b = new Branch();
                    List<JToken> branchAttribs = (r.Children()).ToList();
                    branchAttribs.ForEach(p =>
                    {
                        if (_b.name == null)
                            _b.name = (((JProperty)p).Name == "name") ? ((JProperty)p).Value.ToString() : null;
                        if (_b.objectId == null)
                            _b.objectId = (((JProperty)p).Name == "objectId") ? ((JProperty)p).Value.ToString() : null;
                    });
                    repoBranches.Add(_b);
                });
                Repository.branches = repoBranches;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new UnauthorizedAccessException();
            }
            else
            {
                Console.WriteLine("{0}:{1}", response.StatusCode, response.ReasonPhrase);
            }
        }

        private static void ListProjects(AuthenticationHeaderValue authHeader)
        {
            // use the httpclient
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(azureDevOpsOrganizationUrl);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", "ManagedClientConsoleAppSample");
                client.DefaultRequestHeaders.Add("X-TFS-FedAuthRedirect", "Suppress");
                client.DefaultRequestHeaders.Authorization = authHeader;

                // connect to the REST endpoint            
                HttpResponseMessage response = client.GetAsync("_apis/projects?stateFilter=All&api-version=5.0").Result;

                // check to see if we have a succesfull respond
                if (response.IsSuccessStatusCode)
                {
                    var result = response.Content.ReadAsStringAsync().Result;
                    Console.WriteLine(result);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException();
                }
                else
                {
                    Console.WriteLine("{0}:{1}", response.StatusCode, response.ReasonPhrase);
                }
            }
        }
    }
}
