#region License

// SLNTools
// Copyright (c) 2009
// by Christian Warren
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
// documentation files (the "Software"), to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and
// to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or substantial portions
// of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED
// TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
// CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

#endregion

using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace CWDev.SLNTools.Core.Filter
{
    using Merge;

    public delegate bool AcceptDifferencesHandler(NodeDifference difference);

    public class FilterFile
    {
        public static FilterFile FromFile(string filterFullPath)
        {
            using (var stream = new FileStream(filterFullPath, FileMode.Open, FileAccess.Read))
            {
                return FromStream(filterFullPath, stream);
            }
        }

        public static FilterFile FromStream(string filterFullPath, Stream stream)
        {
            var filterFile = new FilterFile
                {
                    FilterFullPath = filterFullPath
                };

            var xmldoc = new XmlDocument();
            xmldoc.Load(stream);

            var configNode = xmldoc.SelectSingleNode("Config");

            var sourceSlnNode = configNode.SelectSingleNode("SourceSLN");
            filterFile.SourceSolutionFullPath = Path.Combine(
                        Path.GetDirectoryName(filterFullPath),
                        Path.GetFileName(sourceSlnNode.InnerText));

            XmlNode watchForChangesNode = configNode.SelectSingleNode("WatchForChangesOnFilteredSolution");
            if (watchForChangesNode != null)
            {
                filterFile.WatchForChangesOnFilteredSolution = bool.Parse(watchForChangesNode.InnerText);
            }
            XmlNode copyReSharperFilesNode = configNode.SelectSingleNode("CopyReSharperFiles");
            if (copyReSharperFilesNode != null)
            {
                filterFile.CopyReSharperFiles = bool.Parse(copyReSharperFilesNode.InnerText);
            }

            foreach (XmlNode node in configNode.SelectNodes("ProjectToKeep"))
            {
                filterFile.ProjectsToKeep.Add(node.InnerText);
            }

            return filterFile;
        }


        private FilteredSolutionWatcher m_watcher;

        public FilterFile()
        {
            this.SourceSolutionFullPath = null;
            this.FilterFullPath = null;
            this.ProjectsToKeep = new List<string>();
            this.WatchForChangesOnFilteredSolution = false;
            m_watcher = null;
        }

        public string SourceSolutionFullPath { get; set; }

        public SolutionFile SourceSolution
        {
            get { return SolutionFile.FromFile(this.SourceSolutionFullPath); }
        }

        public string FilterFullPath { get; set; }

        public string DestinationSolutionFullPath
        {
            get { return Path.ChangeExtension(this.FilterFullPath, ".sln"); }
        }

        public List<string> ProjectsToKeep { get; private set; }

        public bool WatchForChangesOnFilteredSolution { get; set; }

        public bool CopyReSharperFiles { get; set; }

        public SolutionFile Apply()
        {
            return ApplyOn(this.SourceSolution);
        }

        public SolutionFile ApplyOn(SolutionFile original)
        {
            var includedProjects = new List<Project>();
            foreach (var projectFullName in this.ProjectsToKeep)
            {
                var projectToKeep = original.Projects.FindByFullName(projectFullName);
                if (projectToKeep != null)
                {
                    AddRecursiveDependenciesToList(includedProjects, projectToKeep);
                    foreach (var descendant in projectToKeep.AllDescendants)
                    {
                        AddRecursiveDependenciesToList(includedProjects, descendant);
                    }
                }
                else
                {
                    // TODO MessageBox Found project X in filter but doesn't exist in original solution
                }
            }

            return new SolutionFile(
                        this.DestinationSolutionFullPath,
                        original.Headers,
                        includedProjects,
                        original.GlobalSections);
        }

        private static void AddRecursiveDependenciesToList(List<Project> includedProjects, Project project)
        {
            if (includedProjects.Contains(project))
                return;

            includedProjects.Add(project);
            foreach (var dependency in project.Dependencies)
            {
                AddRecursiveDependenciesToList(includedProjects, dependency);
            }
        }

        public void StartFilteredSolutionWatcher(SolutionFile filteredSolution, AcceptDifferencesHandler handler)
        {
            if (this.WatchForChangesOnFilteredSolution && (m_watcher == null))
            {
                m_watcher = new FilteredSolutionWatcher(
                            handler,
                            this,
                            filteredSolution);
                m_watcher.Start();
            }
        }

        public void StopFilteredSolutionWatcher()
        {
            if (m_watcher != null)
            {
                m_watcher.Stop();
                m_watcher = null;
            }
        }

        public void Save()
        {
            SaveAs(this.FilterFullPath);
        }

        public void SaveAs(string filterFullPath)
        {
            var docFilter = new XmlDocument();

            XmlNode configNode = docFilter.CreateElement("Config");
            docFilter.AppendChild(configNode);

            XmlNode sourceSlnNode = docFilter.CreateElement("SourceSLN");
            sourceSlnNode.InnerText = Path.GetFileName(this.SourceSolutionFullPath);
            configNode.AppendChild(sourceSlnNode);

            XmlNode watchForChangesNode = docFilter.CreateElement("WatchForChangesOnFilteredSolution");
            watchForChangesNode.InnerText = this.WatchForChangesOnFilteredSolution.ToString();
            configNode.AppendChild(watchForChangesNode);

            XmlNode copyReSharperFilesNode = docFilter.CreateElement("CopyReSharperFiles");
            copyReSharperFilesNode.InnerText = this.CopyReSharperFiles.ToString();
            configNode.AppendChild(copyReSharperFilesNode);

            foreach (var projectFullName in this.ProjectsToKeep)
            {
                XmlNode node = docFilter.CreateElement("ProjectToKeep");
                node.InnerText = projectFullName;
                configNode.AppendChild(node);
            }

            docFilter.Save(filterFullPath);
        }
    }
}
