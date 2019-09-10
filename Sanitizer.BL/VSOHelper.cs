using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;


namespace Sanitizer.BL
{
    public partial class VSOHelper
    {
        private string RootUri = "https://microsoftit.visualstudio.com/OneITVso";
        public void GetAllProjects(string VSORootUri)
        {
            string ProjectsUri = String.Concat(new string[]{ VSORootUri, "_apis/projects?api-version=5.0" });
            using (HttpClient client = new HttpClient())
            {
                var projectsJson = client.GetAsync(ProjectsUri);
                string data = projectsJson.Result.Content.ReadAsStringAsync().Result;
            }
        }
    }
}
