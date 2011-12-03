using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using ScrewTurn.Wiki.PluginFramework;

namespace RenderPages
{    
    public class Plugin : IFormatterProviderV30 
    {
        private static readonly ComponentInformation info = new ComponentInformation("RenderPages Plugin", "Kenneth Scott",
                                        "3.1.0.1", "https://github.com/kennethscott/RenderPages", "");
        private static readonly string defaultCss =
            "@page { prince-shrink-to-fit: auto; size: 8.5in 11in; margin: .75in .75in .75in 1in; } " +
            "@page page { @bottom-center { content: counter(page); } } " +
            "@media screen { div.toc, div.page { border-top: thin solid black; padding-top: 1.5em; margin-top: 1.5em; } } " +
            "html { hyphens: auto; prince-text-replace: \"'\" \"\\2019\"; } " +
            "div.toc, div.page { page-break-before: always; } " +
            "h3.category { font-size: 300%; } " +
            "h3.pageHeader { font-size: 150%; padding-bottom: 5px; } " +
            "#tocItems, #tocItems div.tocCategory { display: table; width: 100%; } " +
            "#tocItems a.category { font-size: 125%; } " +
            "#tocItems div.tocCategory a.page span { padding-left: 20px; } " +
            ".toc a { display: table-row; text-align: left; } " +
            ".toc a span { padding-top: 1em; display: table-cell; page-break-inside: avoid; } " +
            ".toc a span:after { content: leader(\".\"); } " +
            ".toc a:after { content: target-counter(attr(href), page); display: table-cell; text-align: right; vertical-align: bottom; } " +
            "div.first.page { counter-reset: page 1; } " +
            "div.page { page: page; } " +
            "#PrintLinkDiv, #BreadcrumbsDiv { display: none; } ";
        private IHostV30 host;
        private string cssFromConfig = String.Empty;

        public int ExecutionPriority
        {
            get { return 50; }
        }

        public string Format(string raw, ContextInformation context, FormattingPhase phase)
        {
            try
            {                                
                MatchCollection colM = Regex.Matches(raw, @"\{RenderPages(.*?)\=(.*?)\}", RegexOptions.Singleline | RegexOptions.Compiled);
                foreach (Match m in colM)
                {
                    ArrayList al = new ArrayList();
                    al.AddRange(m.Groups[2].Value.Split(':'));

                    // fix per lum for v3 compatibility - assume current namespace
                    string currentNamespace, currentPagename = String.Empty;
                    NameTools.ExpandFullName(context.Page.FullName, out currentNamespace, out currentPagename);
                    NamespaceInfo nsiCurrentNamespace = host.FindNamespace(currentNamespace);

                    StringBuilder sbAllPages = new StringBuilder();
                    StringBuilder sbTOC = new StringBuilder();

                    if (!String.IsNullOrEmpty(cssFromConfig))
                        sbTOC.AppendFormat("<style type='text/css'>{0}</style>", cssFromConfig);

                    sbTOC.Append("<div class='toc'>");
                    sbTOC.Append("<h2>Table of Contents</h2>");
                    sbTOC.Append("<div id='tocItems'>");

                    int pageNum = 0;

                    switch (m.Groups[1].Value.TrimStart(' ').Substring(0, 1).ToLower())
                    {
                        // exclude pages 
                        case "p":
                            foreach (PageInfo Pg in host.GetPages(nsiCurrentNamespace)) 
                            {   
                                // ensure current RenderPages page isn't included
                                if (!al.Contains(Pg.FullName) && Pg.FullName != context.Page.FullName)
                                {                                    
                                    sbTOC.Append(formatTocItem(pageNum, Pg.FullName, false));
                                    sbAllPages.Append(formatPage(pageNum, formatPageHeader(pageNum, Pg.FullName), host.GetFormattedContent(Pg)));
                                    pageNum++;
                                }
                            }
                            break;
                        // include categories 
                        case "c":
                            foreach (CategoryInfo ci in host.GetCategories(nsiCurrentNamespace))
                            {
                                if (al.Contains("#ALL#") || al.Contains(ci.FullName))
                                {                                    
                                    sbTOC.Append("<div class='tocCategory'>" + formatTocItem(pageNum, ci.FullName, true));
                                    sbAllPages.Append(formatPage(pageNum, formatCategoryHeader(pageNum, ci.FullName), String.Empty));
                                    pageNum++;
                                    
                                    // fix per jantony to ensure alpha sorting of pages
                                    Array.Sort(ci.Pages);
                                    foreach (string strPage in ci.Pages)
                                    {
                                        // ensure current RenderPages page isn't included
                                        if (strPage != context.Page.FullName)
                                        {
                                            PageInfo Pg = host.FindPage(strPage);                                            
                                            sbTOC.Append(formatTocItem(pageNum, Pg.FullName, false));
                                            sbAllPages.Append(formatPage(pageNum, formatPageHeader(pageNum, Pg.FullName), host.GetFormattedContent(Pg)));
                                            pageNum++;
                                        }
                                    }

                                    sbTOC.Append("</div>");
                                }
                            }
                            break;
                        default:
                            break;
                    }
                    raw = Regex.Replace(raw, m.Value, sbTOC.ToString() + "</div></div>" + sbAllPages.ToString(),
                                    RegexOptions.Singleline | RegexOptions.Compiled);
                }
                return raw;
            }
            catch (Exception ex)
            {
                host.LogEntry(String.Format("Failed RenderPages Format method for page {0} - {1}", context.Page.FullName, ex.Message),
                                LogEntryType.Error, null, this);
                return "Unexpected problem encountered in RenderPages plugin: " + ex.Message;
            }
        }

        public bool PerformPhase1
        {
            get { return false; }
        }
        public bool PerformPhase2
        {
            get { return false; }
        }
        public bool PerformPhase3
        {
            get { return true; }
        }
        public string PrepareTitle(string title, ContextInformation context)
        {
            return title;
        }

        public string ConfigHelpHtml
        {
            get { return "Enter desired CSS for page formatting.  Delete all contents then disable and re-enable to reset."; }
        }

        public ComponentInformation Information
        {
            get { return info; }
        }

        public void Init(IHostV30 host, string config)
        {
            this.host = host;

            // if no css in config, default to hardcoded defaults
            if (String.IsNullOrEmpty(config))
            {
                host.SetProviderConfiguration(this, defaultCss);
                config = defaultCss;
            }
            this.cssFromConfig = config;
        }
        
        public void Shutdown() { }    

        #region Page Formatting      
        private string formatPageHeader(int pageNum, string pageName)
        {
            return String.Format("<h3 class='pageHeader'><a id='pg{0}' href='{1}.ashx'>{1}</a></h3>", pageNum.ToString(), pageName);
        }
        private string formatPage(int pageNum, string pageHeader, string pageContent) 
        {
            return String.Format("<div class='page{0}'>{1}{2}</div>", pageNum==0 ? " first" : String.Empty,
                                    pageHeader, pageContent.Replace("{TOC}", String.Empty));
        }
        private string formatCategoryHeader(int pageNum, string category)
        {
            return String.Format("<h3 class='category' id='pg{0}'>{1}</h3>", pageNum.ToString(), category);
        }
        private string formatTocItem(int pageNum, string pageName, bool category)
        {
            return String.Format("<a href='#pg{0}' class='{1}'><span>{2}</span></a>", pageNum.ToString(), category ? "category" : "page", pageName);
        }
        #endregion
    }
}
