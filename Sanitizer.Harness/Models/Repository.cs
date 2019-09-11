using System;
using System.Collections.Generic;

namespace Sanitizer.Harness.Models
{
    public class Repository
    {
        public string id { get; set; }
        public string name { get; set; }
        public string url { get; set; }
        public Project project { get; set; }

        public List<Branch> branches { get; set; }
        public string defaultBranch { get; set; }
        public int size { get; set; }
        public string remoteUrl { get; set; }
        public string sshUrl { get; set; }
        public string webUrl { get; set; }
    }

    public class Project
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string url { get; set; }
        public string state { get; set; }
        public int revision { get; set; }
        public string visibility { get; set; }
        public DateTime lastUpdateTime { get; set; }
    }

    public class Branch
    {
        public string name { get; set; }
        public string objectId { get; set; }
        public List<Build> builds { get; set; }
    }

    public class Build
    {
        public string id { get; set; }
        public string name { get; set; }
        public string url { get; set; }

        public string number { get; set; }
    }

    public class Release
    {
        public string id { get; set; }
        public string name { get; set; }
        public string url { get; set; }
        public DateTime modifiedOn { get; set; }
        public bool isDeleted { get; set; }
    }

}


