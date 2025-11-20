using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TableSpans.HtmlAgilityPack;


namespace Taxxor.Project
{

    /// <summary>
    /// Custom Project Data Store logic for Royal Philips
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Royal Philips custom filing section load logic
        /// </summary>
        /// <param name="xmlSection"></param>
        /// <param name="projectVars"></param>
        /// <param name="xmlSectionFolderPathOs"></param>
        /// <returns></returns>
        public static XmlDocument PhilipsFilingComposerDataGet(XmlDocument xmlSection, ProjectVariables projectVars, string xmlSectionFolderPathOs)
        {
            var timeRoutine = (siteType == "local");
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

            //
            // => Custom logic for Taxes Paid
            //
            switch (projectVars.projectId)
            {
                case "ar20":
                case "ar21":
                case "ar22":
                    if (projectVars.outputChannelVariantId == "taxespaid")
                    {
                        xmlSection.ReplaceContent(TaxesPaidCustomLogicHistorical(xmlSection, projectVars, xmlSectionFolderPathOs), true);
                    }
                    break;

                case "ar23":
                case "ar24":
                case "ar25":
                    if (projectVars.outputChannelVariantId == "taxespaid")
                    {
                        xmlSection.ReplaceContent(TaxesPaidCustomLoadLogic(xmlSection, projectVars, xmlSectionFolderPathOs), true);
                    }
                    break;
            }

            //
            // => On-the-fly conversion of content to assure that comparison with old-style XHTML continues to work
            //
            if (projectVars.outputChannelType == "pdf")
            {
                switch (projectVars.projectId)
                {
                    case "q119":
                    case "q219":
                    case "q319":
                    case "q419":
                    case "q120":
                    case "q220":
                    case "q320":
                    case "q420":
                    case "ar19":
                    case "ar20":
                        if (timeRoutine) watch.Start();

                        // Run the content through the conversion stylesheet and assure that whatever we return is valid HTML (no self closing tags, etc.)
                        xmlSection = ValidHtmlVoidTags(TransformXmlToDocument(xmlSection, $"{applicationRootPathOs}/backend/code/custom/sectiondataload-royalphilips.xsl"));

                        if (timeRoutine)
                        {
                            watch.Stop();
                            appLogger.LogInformation($"Total conversion time: {watch.ElapsedMilliseconds} ms");
                        }
                        break;
                }
            }


            return xmlSection;
        }

        /// <summary>
        /// Royal Philips custom filing section save logic
        /// </summary>
        /// <param name="xmlSection"></param>
        /// <param name="projectVars"></param>
        /// <param name="xmlSectionFilePathOs"></param>
        /// <returns></returns>
        public static XmlDocument PhilipsFilingComposerDataSave(XmlDocument xmlSection, ProjectVariables projectVars, string xmlSectionFilePathOs)
        {
            var timeRoutine = (siteType == "local");
            //System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

            //
            // => Custom logic for Taxes Paid
            //
            switch (projectVars.projectId)
            {

                case "ar23":
                    if (projectVars.outputChannelVariantId == "taxespaid")
                    {
                        xmlSection.ReplaceContent(TaxesPaidCustomSaveLogic(xmlSection, projectVars, xmlSectionFilePathOs), true);
                    }
                    break;
            }


            return xmlSection;
        }

        /// <summary>
        /// Special code that is executed when a Taxes Paid section is saved
        /// </summary>
        /// <param name="xmlSection"></param>
        /// <param name="projectVars"></param>
        /// <param name="xmlSectionFolderPathOs"></param>
        /// <returns></returns>
        public static XmlDocument TaxesPaidCustomSaveLogic(XmlDocument xmlSection, ProjectVariables projectVars, string xmlSectionFolderPathOs)
        {
            var timeRoutine = (siteType == "local");
            //System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            Console.WriteLine($"TaxesPaidCustomSaveLogic: {projectVars.outputChannelVariantId}");

            try
            {
                //
                // => Work on the country detail page
                //
                var nodeWrapper = xmlSection.SelectSingleNode("//div[contains(@class, 'taxes-paid-country')]");
                if (nodeWrapper != null)
                {
                    // if (timeRoutine) watch.Start();

                    //
                    // => Cleanup the content that the templating system injects into the page that is rendered in the editor
                    //
                    var nodeTemplateContentWrapper = nodeWrapper.SelectSingleNode(".//div[@class = 'tp-generated-content']");
                    if (nodeTemplateContentWrapper != null)
                    {
                        nodeTemplateContentWrapper.InnerXml = "...";
                    }

                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "There was an error preparing Taxes Paid content");
            }

            return xmlSection;
        }



        /// <summary>
        /// Special code that is executed when a Taxes Paid section is loaded
        /// </summary>
        /// <param name="xmlSection"></param>
        /// <param name="projectVars"></param>
        /// <param name="xmlSectionFolderPathOs"></param>
        /// <returns></returns>
        public static XmlDocument TaxesPaidCustomLoadLogic(XmlDocument xmlSection, ProjectVariables projectVars, string xmlSectionFolderPathOs)
        {
            var timeRoutine = (siteType == "local" || siteType == "dev");
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();


            try
            {
                //
                // => Work on the country detail page
                //
                var nodeWrapper = xmlSection.SelectSingleNode("//div[contains(@class, 'taxes-paid-country')]");
                if (nodeWrapper != null)
                {
                    if (timeRoutine) watch.Start();

                    //
                    // => Inject the values from the large oveview table into the country detail page
                    //
                    var nodeArticle = xmlSection.SelectSingleNode("/data/content[@lang='en']/article");
                    var articleId = nodeArticle.GetAttribute("id");
                    var mainTitle = nodeArticle.SelectSingleNode("descendant::h1")?.InnerText ?? "unknown";
                    var nodeSection = nodeArticle.SelectSingleNode("descendant::section");

                    var xmlSteeringTable = new XmlDocument();
                    var pathXmlSteeringTable = $"{xmlSectionFolderPathOs}/cat-large-overview-table.xml";
                    if (Path.Exists(pathXmlSteeringTable))
                    {
                        //
                        // => Load the steering table
                        //
                        xmlSteeringTable.Load(pathXmlSteeringTable);
                        // appLogger.LogInformation($" => Load takes: {watch.ElapsedMilliseconds} ms");


                        // Console.WriteLine($"Loaded XML Steering Table: {pathXmlSteeringTable}");

                        //
                        // => Retrieve the data reference (filename) of the country detail page so that we can look it up in the large overview table
                        //
                        var nodeSectionMetadata = XmlCmsContentMetadata.SelectSingleNode($"/projects/cms_project[@id='{projectVars.projectId}']/data/content[@datatype='sectiondata' and  metadata/entry[@key='ids']='{articleId},{articleId}']");
                        if (nodeSectionMetadata != null)
                        {
                            var dataReference = nodeSectionMetadata.GetAttribute("ref");
                            // Console.WriteLine($"Data Reference: {dataReference}");



                            var nodeTableRow = xmlSteeringTable.SelectSingleNode($"/data/content/article[1]//section//table/tbody/tr[@data-ref='{dataReference}']");
                            if (nodeTableRow != null)
                            {
                                //
                                // => Only leave the relevant rows in the table to speed up the SDE syncronization process
                                //
                                var nodeListIrrelevantRows = xmlSteeringTable.SelectNodes($"/data/content/article[1]//section//table/tbody/tr[not(@data-ref='{dataReference}')]");
                                RemoveXmlNodes(nodeListIrrelevantRows);

                                //
                                // => Sync the Structured Data Element values with the XML file
                                //
                                try
                                {
                                    xmlSteeringTable.ReplaceContent(SyncStructuredDataElements(xmlSteeringTable, pathXmlSteeringTable, projectVars.projectId), true);
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError($"Unable to sync SDE values into the large overview table: {ex.Message}");
                                    throw;
                                }
                                // appLogger.LogInformation($" => Sync takes: {watch.ElapsedMilliseconds} ms");


                                //
                                // Use the header row to categorize the SDE's in the table similar to how they are organized in the table itself
                                //
                                var categories = new List<string>();
                                var nodeCategoryRow = xmlSteeringTable.SelectSingleNode("/data/content/article[1]//section//table/thead/tr[2]");
                                if (nodeCategoryRow != null)
                                {
                                    var nodeListCells = nodeCategoryRow.SelectNodes("descendant::th[@colspan | @data-grouptype]");
                                    foreach (XmlNode nodeCell in nodeListCells)
                                    {
                                        var category = nodeCell.InnerText.Trim();
                                        if (!categories.Contains(category)) categories.Add(category);

                                        _ = int.TryParse(nodeCell.GetAttribute("colspan") ?? "", out int colSpan);
                                        var numberOfCellsToBeInserted = colSpan - 1;

                                        for (int i = 0; i < numberOfCellsToBeInserted; i++)
                                        {
                                            var nodeCellToBeInserted = xmlSteeringTable.CreateElementWithText("th", category);
                                            nodeCategoryRow.InsertAfter(nodeCellToBeInserted, nodeCell);
                                        }
                                    }
                                }
                                // Console.WriteLine($"XML label row: {nodeLabelRow.OuterXml}");

                                //
                                // => Enrich the cells from the row of the Country Detail page with the information from the header cells
                                //
                                var nodeHeaderRow = xmlSteeringTable.SelectSingleNode("/data/content/article[1]//section//table/thead/tr[4]");
                                if (nodeHeaderRow != null)
                                {
                                    // // Dump the row
                                    // Console.WriteLine($"Header row:\n{PrettyPrintXml(nodeHeaderRow)}");
                                    var count = 0;
                                    foreach (XmlNode nodeHeaderCell in nodeHeaderRow.SelectNodes("th"))
                                    {
                                        count++;
                                        var nodeCell = nodeTableRow.SelectSingleNode($"td[{count}]");
                                        var nodeCategoryCell = nodeCategoryRow.SelectSingleNode($"th[{count}]");
                                        if (nodeCell != null)
                                        {
                                            nodeCell.SetAttribute("category", nodeCategoryCell.InnerText);
                                            nodeCell.SetAttribute("label", nodeHeaderCell.InnerText);
                                            nodeCell.SetAttribute("data-labelref", nodeHeaderCell.GetAttribute("data-labelref"));
                                        }
                                        else
                                        {
                                            appLogger.LogError($"Could not find cell {count} in row {nodeHeaderRow.OuterXml}");
                                        }

                                    }
                                }

                                // Dump the row
                                // Console.WriteLine($"Xml row:\n{PrettyPrintXml(nodeTableRow)}");

                                //
                                // => Check if this country detail page is part of the full PDF or not
                                //
                                var includeInFullPdf = true;

                                var nodeCheckBox = nodeTableRow.SelectSingleNode("td/input[@data-tyoe='pdfinclude']");
                                if (nodeCheckBox != null)
                                {
                                    includeInFullPdf = nodeCheckBox.HasAttribute("checked");

                                    // Set a marker on the section node so that we can style something special in the PDF     
                                    if (!includeInFullPdf)
                                    {
                                        nodeSection?.SetAttribute("data-hidefromfullpdf", "true");
                                    }
                                    else
                                    {
                                        if (nodeSection?.HasAttribute("data-hidefromfullpdf") ?? false) nodeSection.RemoveAttribute("data-hidefromfullpdf");
                                    }
                                }
                                else
                                {
                                    appLogger.LogWarning($"No checkbox found for article: {articleId}");
                                }
                                // appLogger.LogInformation($"Include in full PDF: {includeInFullPdf}, articleId: {articleId}");


                                //
                                // => Render the JSON to be injected in the country detail page
                                //
                                dynamic jsonData = new ExpandoObject();
                                jsonData.geography = nodeTableRow.SelectSingleNode("td[2]")?.InnerText ?? "unknown";
                                jsonData.country = nodeTableRow.SelectSingleNode("td[3]")?.InnerText ?? "unknown";
                                jsonData.show = includeInFullPdf;
                                jsonData.data = new Dictionary<string, dynamic>();

                                for (int i = 0; i < categories.Count; i++)
                                {
                                    var categoryKey = $"category{i + 1}";
                                    jsonData.data.Add(categoryKey, new ExpandoObject());
                                    jsonData.data[categoryKey].label = categories[i];
                                    jsonData.data[categoryKey].datapoints = new List<dynamic>();

                                    var nodeListCells = nodeTableRow.SelectNodes($"descendant::td[@category='{categories[i]}']");
                                    foreach (XmlNode nodeCell in nodeListCells)
                                    {
                                        var cellText = nodeCell.InnerText.Trim();
                                        if (cellText.Contains('*')) cellText = "";
                                        dynamic jsonDataPoint = new ExpandoObject();
                                        jsonDataPoint.key = nodeCell.GetAttribute("data-labelref");
                                        jsonDataPoint.value = cellText;
                                        jsonDataPoint.label = nodeCell.GetAttribute("label");
                                        jsonDataPoint.factid = nodeCell.SelectSingleNode("span[@data-fact-id]")?.GetAttribute("data-fact-id") ?? "";
                                        jsonData.data[categoryKey].datapoints.Add(jsonDataPoint);
                                    }

                                }

                                // Convert to JSON
                                string jsonToReturnCategorized = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);
                                // Console.WriteLine($"---------------------------\nCategorized JSON to return:\n{jsonToReturnCategorized}\n------------------------");


                                // Inject it in the content
                                var nodeJsonContentWrapper = xmlSection.CreateElementWithText("div", jsonToReturnCategorized);
                                nodeJsonContentWrapper.SetAttribute("data-cat-content", "values");
                                nodeJsonContentWrapper.SetAttribute("data-stripcontentonsave", "true");
                                nodeJsonContentWrapper.SetAttribute("class", "data hidden");
                                nodeSection.AppendChild(nodeJsonContentWrapper);
                            }
                            else
                            {
                                appLogger.LogError($"Could not find row for data reference: {dataReference}");
                            }

                        }
                        else
                        {
                            appLogger.LogWarning($"Unable to locate country detail page section metadata {articleId}");
                        }

                    }
                    else
                    {
                        appLogger.LogError($"Unable to load the large overview steering table file: {Path.GetFileName(pathXmlSteeringTable)}");
                    }

                    //
                    // => Render the question data JSON to be injected in the country detail page
                    //
                    var xmlPluginQuestionsData = new XmlDocument();
                    var pathXmlPluginQuestionsData = $"{xmlSectionFolderPathOs}/tax-country-descriptions.xml";
                    if (Path.Exists(pathXmlPluginQuestionsData))
                    {
                        dynamic jsonData = new ExpandoObject();



                        jsonData.header = "Tax summary";
                        jsonData.free = new ExpandoObject();
                        jsonData.free.title = "Box 1 - Open (descriptive section, explaining tax system and/or specific tax situation of Philips in the country)";

                        jsonData.ectr = new Dictionary<string, dynamic>(); ;
                        jsonData.ctr = new Dictionary<string, dynamic>(); ;
                        jsonData.itc = new Dictionary<string, dynamic>(); ;

                        xmlPluginQuestionsData.Load(pathXmlPluginQuestionsData);

                        var nodeListTableWrappers = xmlPluginQuestionsData.SelectNodes("/data/content/article[1]//section//div[@id]");
                        // appLogger.LogCritical($"Found {nodeListTableWrappers.Count} table wrappers");

                        foreach (XmlNode nodeTableWrapper in nodeListTableWrappers)
                        {
                            var tableType = nodeTableWrapper.SelectSingleNode("div[1]/div[1]/p")?.InnerText ?? "unknown";
                            var tableTitle = nodeTableWrapper.SelectSingleNode("table/thead/tr/th")?.InnerText ?? "unknown";
                            // appLogger.LogCritical($"Processing {tableType} table");

                            // Loop through the rows
                            var dictCategories = new Dictionary<string, Dictionary<string, dynamic>>();
                            var alphabetNumber = 1;
                            var nodeListTableRows = nodeTableWrapper.SelectNodes("table/tbody/tr");
                            var currentMainCategory = "";
                            var currentSubCategory = "";
                            foreach (XmlNode nodeTableRow in nodeListTableRows)
                            {
                                string? questionGuid = nodeTableRow.GetAttribute("data-guid");
                                var nodeListRowCells = nodeTableRow.SelectNodes("td");
                                var firstCell = nodeListRowCells.Item(0);
                                if (firstCell.HasAttribute("data-main-category"))
                                {
                                    currentMainCategory = firstCell.InnerText;
                                    alphabetNumber = 1;

                                    if (nodeListRowCells.Item(1).HasAttribute("data-sub-category"))
                                    {
                                        currentSubCategory = nodeListRowCells.Item(1).InnerText;
                                        dictCategories.Add(currentMainCategory, new Dictionary<string, dynamic>());
                                        dictCategories[currentMainCategory].Add(currentSubCategory, nodeListRowCells.Item(2).InnerText);
                                    }
                                    else
                                    {
                                        dictCategories.Add(currentMainCategory, new Dictionary<string, dynamic>());
                                        dictCategories[currentMainCategory].Add(questionGuid, nodeListRowCells.Item(1).InnerText);
                                    }


                                }
                                else if (firstCell.HasAttribute("data-sub-category"))
                                {

                                    currentSubCategory = firstCell.InnerText;
                                    alphabetNumber = 1;

                                    dictCategories[currentMainCategory].Add(currentSubCategory, new ExpandoObject());
                                    dictCategories[currentMainCategory][currentSubCategory] = new Dictionary<string, string>();
                                    dictCategories[currentMainCategory][currentSubCategory].Add(questionGuid, nodeListRowCells.Item(1).InnerText);
                                }
                                else if (!string.IsNullOrEmpty(currentSubCategory) && nodeListRowCells.Count == 1)
                                {
                                    dictCategories[currentMainCategory][currentSubCategory].Add(questionGuid, nodeListRowCells.Item(0).InnerText);
                                }
                                else
                                {
                                    dictCategories[currentMainCategory].Add(questionGuid, firstCell.InnerText);
                                }
                                alphabetNumber++;

                            }


                            switch (tableType)
                            {
                                case "ectr":
                                    jsonData.ectr.Add("title", tableTitle);

                                    foreach (var item in dictCategories)
                                    {
                                        jsonData.ectr.Add(item.Key, item.Value);
                                    }
                                    break;

                                case "ctr":
                                    jsonData.ctr.Add("title", tableTitle);

                                    foreach (var item in dictCategories)
                                    {
                                        jsonData.ctr.Add(item.Key, item.Value);
                                    }
                                    break;

                                case "itc":
                                    jsonData.itc.Add("title", tableTitle);

                                    foreach (var item in dictCategories)
                                    {
                                        jsonData.itc.Add(item.Key, item.Value);
                                    }
                                    break;

                            }

                            // jsonData[tableTitle] = new ExpandoObject();
                        }



                        // Convert to JSON
                        string jsonToReturnCategorized = JsonConvert.SerializeObject(jsonData, Newtonsoft.Json.Formatting.Indented);
                        // Console.WriteLine($"---------------------------\nJSON to return:\n{jsonToReturnCategorized}\n------------------------");

                        // Inject it in the content
                        var nodeJsonContentWrapper = xmlSection.CreateElementWithText("div", jsonToReturnCategorized);
                        nodeJsonContentWrapper.SetAttribute("data-cat-content", "questions");
                        nodeJsonContentWrapper.SetAttribute("data-stripcontentonsave", "true");
                        nodeJsonContentWrapper.SetAttribute("class", "data hidden");
                        nodeSection.AppendChild(nodeJsonContentWrapper);
                    }
                    else
                    {
                        appLogger.LogError($"Unable to locate the questions data file: {Path.GetFileName(pathXmlSteeringTable)}");
                    }

                    // Console.WriteLine($"** {mainTitle} # of articles: {xmlSection.SelectNodes("/data/content/article").Count} **");

                    if (timeRoutine)
                    {
                        watch.Stop();
                        appLogger.LogInformation($"Country detail page processing time: {watch.ElapsedMilliseconds} ms");
                    }
                }


                //
                // => Rebuild the large Taxes Paid country pages overview table
                //
                var nodeCountryOverviewArticle = xmlSection.SelectSingleNode("/data/content/article[@id='taxes-paid-country-overview']");
                if (nodeCountryOverviewArticle != null)
                {

                    // var retrieveSdeValuesFromLocalCache = true;
                    // var retrieveCountryDetailPagesFromHierarchy = false;


                    /*
                    //
                    // => Check if we can use the cache
                    //
                    var validSortedTableCache = false;
                    var cacheKeySortedTable = RenderCatCacheKey(projectVars.projectId, "taxes-paid-country-overview");

                    if (useMemoryCache && MemoryCacheItemExists(cacheKeySortedTable))
                    {
                        var xmlData = (string)RetrieveMemoryCacheItem(cacheKeySortedTable);
                        if (!string.IsNullOrEmpty(xmlData))
                        {
                            xmlSortedTable.LoadXml(xmlData);
                            validSortedTableCache = true;
                        }
                    }


                    //
                    // => Generate the sorted table
                    //
                    if (!validSortedTableCache)
                    {

                    //
                        // => Update the value in the cache
                        //
                        if (useMemoryCache)
                        {
                            SetMemoryCacheItem(cacheKeySortedTable, xmlSortedTable.OuterXml, TimeSpan.FromHours(24));
                        }
                    }
                    */


                    if (timeRoutine) watch.Start();
                    RequestVariables reqVars = RetrieveRequestVariables(System.Web.Context.Current);


                    var nodeArticle = xmlSection.SelectSingleNode("/data/content[@lang='en']/article");
                    var nodeSection = nodeArticle.SelectSingleNode("descendant::section");

                    //
                    // Determine the folder where the data lives
                    //
                    var cmsDataRootPathOs = RetrieveFilingDataFolderPathOs(projectVars.projectId);

                    // Loop through the data files
                    if (!Directory.Exists(cmsDataRootPathOs)) throw new Exception($"No data folder found ({cmsDataRootPathOs})");


                    //
                    // => Load the XML file containing the CAT datapoints
                    //
                    var largeOverviewTablePathOs = $"{cmsDataRootPathOs}/cat-large-overview-table.xml";
                    var xmlLargeOverviewTable = new XmlDocument();
                    xmlLargeOverviewTable.Load(largeOverviewTablePathOs);


                    //
                    // => Sync the Structured Data Element values with the XML file
                    //
                    try
                    {
                        xmlLargeOverviewTable.ReplaceContent(SyncStructuredDataElements(xmlLargeOverviewTable, largeOverviewTablePathOs, projectVars.projectId), true);
                    }
                    catch (Exception ex)
                    {
                        appLogger.LogError($"Unable to sync SDE values into the large overview table: {ex.Message}");
                        throw;
                    }

                    //
                    // => Load the table containing the datapoints
                    //
                    var nodeTable = xmlLargeOverviewTable.SelectSingleNode("//table[contains(@class, 'cat-datatable-details')]");



                    // - Assure that all the table cells contain a value and a SDE
                    var nodeListSdes = nodeTable.SelectNodes("//tbody//td[not(span[@data-fact-id]) and position() > 3]");
                    foreach (XmlNode nodeSde in nodeListSdes)
                    {
                        var totalValue = "0.00";

                        if (nodeSde.InnerText.Contains("* "))
                        {
                            totalValue = nodeSde.InnerText.Replace("* ", "").Replace(" *", "");
                            // Console.WriteLine($"totalValue => {totalValue}");
                        }
                        else
                        {
                            nodeSde.InnerText = "0.00";
                        }

                        var nodeSpan = xmlLargeOverviewTable.CreateElementWithText("span", totalValue);
                        nodeSpan.SetAttribute("data-fact-id", Guid.NewGuid().ToString());
                        nodeSde.InnerXml = "";
                        nodeSde.AppendChild(nodeSpan);
                    }

                    // - Remove cell contents if an SDE is prepended with a *
                    nodeListSdes = nodeTable.SelectNodes("//tbody//td[span[@data-fact-id] and position() > 3 and contains(text(),'*')]");
                    foreach (XmlNode nodeSde in nodeListSdes)
                    {
                        nodeSde.InnerXml = "";
                    }


                    // Console.WriteLine($"{PrettyPrintXml(xmlLargeOverviewTable.SelectSingleNode("//tbody/tr").OuterXml)}");

                    //
                    // => Retrieve the employee data
                    //
                    var employeeData = new Dictionary<string, XmlNode>();
                    var nodeListEmployeeRows = nodeTable.SelectNodes("tbody/tr");
                    foreach (XmlNode nodeRow in nodeListEmployeeRows)
                    {
                        var countryCode = nodeRow.GetAttribute("data-countrycode") ?? "";
                        var employees = nodeRow.SelectSingleNode("td[last()]/span");
                        employeeData.Add(countryCode, employees);
                    }

                    //
                    // => Remove columns
                    //
                    string[] columnsToRemove = { "ectr", "ctr", "nopf", "cr", "li", "et", "ebe", "empl" };

                    // - Normalize the table
                    var tableSpansExtension = new TableSpansExtension();
                    var htmlDocument = new HtmlDocument
                    {
                        OptionOutputAsXml = true
                    };
                    htmlDocument.LoadHtml(nodeTable.OuterXml);

                    var tableNode = htmlDocument.DocumentNode.SelectSingleNode("table");
                    var normalizedTableNode = tableSpansExtension.ProcessTable(tableNode);

                    var xmlTableNormalized = new XmlDocument();
                    xmlTableNormalized.LoadXml(normalizedTableNode.OuterHtml);


                    // - Find the columns to remove
                    var columnNumbersToRemove = new List<int>
                    {
                        0
                    };
                    var columnCounter = 0;
                    var headerCells = xmlTableNormalized.SelectNodes("/table/thead/tr[4]/th");

                    if (headerCells != null)
                    {
                        foreach (XmlNode headerCell in headerCells)
                        {
                            var labelRef = headerCell.GetAttribute("data-labelref");

                            if (columnsToRemove.Contains(labelRef))
                            {
                                columnNumbersToRemove.Add(columnCounter);
                            }

                            columnCounter++;
                        }
                    }

                    // - Remove the columns
                    var nodeListRows = xmlTableNormalized.SelectNodes("/table/*/tr");
                    foreach (XmlNode nodeRow in nodeListRows)
                    {
                        var nodeListCells = nodeRow.SelectNodes("*");
                        foreach (int columnNumberToRemove in columnNumbersToRemove)
                        {
                            nodeListCells.Item(columnNumberToRemove).SetAttribute("remove", "true");
                        }
                    }
                    RemoveXmlNodes(xmlTableNormalized.SelectNodes("/table/*/tr/*[@remove='true']"));


                    //
                    // => Generate a new sorted table
                    //
                    var xmlSortedTable = new XmlDocument();

                    var nodeTableElement = xmlSortedTable.CreateElement("table");
                    nodeTableElement.SetAttribute("id", nodeTable.GetAttribute("id"));
                    nodeTableElement.SetAttribute("class", "tp-overviewtable");
                    xmlSortedTable.AppendChild(nodeTableElement);

                    var nodeTableHeadElement = xmlSortedTable.CreateElement("thead");
                    nodeTableHeadElement.SetAttribute("data-stripcontentonsave", "true");
                    nodeTableElement.AppendChild(nodeTableHeadElement);

                    // - Import two header rows from the normalized table
                    var nodeFirstRowImported = xmlSortedTable.ImportNode(xmlTableNormalized.SelectSingleNode("/table/thead/tr[2]"), true);
                    nodeTableHeadElement.AppendChild(nodeFirstRowImported);
                    var nodeSecondRowImported = xmlSortedTable.ImportNode(xmlTableNormalized.SelectSingleNode("/table/thead/tr[4]"), true);
                    nodeTableHeadElement.AppendChild(nodeSecondRowImported);

                    // - Modify the imported rows a bit
                    nodeSecondRowImported.SelectNodes("th").Item(0).InnerText = "";
                    nodeSecondRowImported.SelectNodes("th").Item(0).SetAttribute("data-labelref", "country");
                    nodeSecondRowImported.SelectNodes("th").Item(1).InnerText = "Number of employees";
                    nodeSecondRowImported.SelectNodes("th").Item(1).SetAttribute("data-labelref", "empl");

                    // - Inject the table body element
                    var nodeTableBodyElement = xmlSortedTable.CreateElement("tbody");
                    nodeTableBodyElement.SetAttribute("data-stripcontentonsave", "true");
                    nodeTableElement.AppendChild(nodeTableBodyElement);

                    var geographies = new List<string>
                    {
                        "Western Europe",
                        "North America",
                        "Other mature geographies",
                        "Growth geographies"
                    };

                    foreach (string geography in geographies)
                    {
                        // Console.WriteLine($"{geography}");
                        var nodeListGeoRows = xmlTableNormalized.SelectNodes("/table/tbody/tr");

                        // - Inject a special header row in the table that marks the geography
                        var numberOfCells = nodeListGeoRows.Item(0).ChildNodes.Count;
                        var nodeHeaderRow = xmlSortedTable.CreateElement("tr");
                        nodeHeaderRow.SetAttribute("class", "geography");
                        nodeTableBodyElement.AppendChild(nodeHeaderRow);
                        for (int i = 0; i < numberOfCells; i++)
                        {
                            var nodeHeaderCell = xmlSortedTable.CreateElement("td");
                            if (i == 0)
                            {
                                nodeHeaderCell.InnerText = geography;
                            }
                            else
                            {
                                var emptyComment = xmlSortedTable.CreateComment(".");
                                nodeHeaderCell.AppendChild(emptyComment);
                            }
                            nodeHeaderRow.AppendChild(nodeHeaderCell);
                        }


                        // - Find the rows for this geography and store the total tax in a dictionary
                        Dictionary<string, double> dictInfo = new Dictionary<string, double>();

                        foreach (XmlNode nodeRow in nodeListGeoRows)
                        {
                            var nodeFirstCell = nodeRow.SelectSingleNode("td[1]");
                            if (nodeFirstCell.InnerText.Trim() == geography)
                            {
                                var countryCode = nodeRow.GetAttribute("data-countrycode") ?? "";
                                var totalTaxValue = nodeRow.SelectSingleNode("td[last()]").InnerText.Trim();
                                _ = double.TryParse(totalTaxValue, out double doubleTaxValue);

                                dictInfo.Add(countryCode, doubleTaxValue);
                            }

                        }

                        // - Sort the dictionary
                        var sortedDict = dictInfo.OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);

                        // - Import the rows using the order in the sorted dictionary
                        foreach (KeyValuePair<string, double> pair in sortedDict)
                        {
                            // Console.WriteLine("{0}: {1}", pair.Key, pair.Value);


                            var nodeRowToImport = xmlTableNormalized.SelectSingleNode($"/table/tbody/tr[@data-countrycode='{pair.Key}']");
                            var nodeRowImported = xmlSortedTable.ImportNode(nodeRowToImport, true);

                            var nodeFirstCell = nodeRowImported.SelectSingleNode("td[1]");
                            var nodeSecondCell = nodeRowImported.SelectSingleNode("td[2]");

                            // Put the name of the country in the first cell of the row and the HR data in the second cell
                            nodeFirstCell.InnerText = nodeSecondCell.InnerText;

                            // Inject the HR data in the second cell of the row
                            nodeSecondCell.InnerText = "";
                            if (employeeData[pair.Key] != null)
                            {
                                var nodeSpanEmpoyeeData = xmlSortedTable.ImportNode(employeeData[pair.Key], true);
                                nodeSecondCell.AppendChild(nodeSpanEmpoyeeData);
                            }


                            // Add the new row in the tbody element of the sorted table
                            nodeTableBodyElement.AppendChild(nodeRowImported);
                        }
                    }

                    //
                    // => Insert empty columns
                    //
                    int[] columnPositions = new int[] { 1, 7 };
                    var nodeListSortedTableRows = xmlSortedTable.SelectNodes("/table/thead/tr");
                    foreach (XmlNode nodeRow in nodeListSortedTableRows)
                    {
                        foreach (int columnPosition in columnPositions)
                        {
                            var nodeCell = xmlSortedTable.CreateElement("th");
                            nodeCell.SetAttribute("class", "empty");
                            var nodeEmptyComment = xmlSortedTable.CreateComment(".");
                            nodeCell.AppendChild(nodeEmptyComment);
                            nodeRow.InsertAfter(nodeCell, nodeRow.ChildNodes[columnPosition]);
                        }
                    }
                    nodeListSortedTableRows = xmlSortedTable.SelectNodes("/table/tbody/tr");
                    foreach (XmlNode nodeRow in nodeListSortedTableRows)
                    {
                        foreach (int columnPosition in columnPositions)
                        {
                            var nodeCell = xmlSortedTable.CreateElement("td");
                            nodeCell.SetAttribute("class", "empty");
                            var nodeEmptyComment = xmlSortedTable.CreateComment(".");
                            nodeCell.AppendChild(nodeEmptyComment);
                            nodeRow.InsertAfter(nodeCell, nodeRow.ChildNodes[columnPosition]);
                        }
                    }

                    //
                    // => In the sorted table we want to strip the structured data elements
                    //
                    foreach (XmlNode nodeRow in nodeListSortedTableRows)
                    {
                        var nodeListSdeSpans = nodeRow.SelectNodes("td/span[@data-fact-id]");
                        foreach (XmlNode nodeSdeSpan in nodeListSdeSpans)
                        {
                            nodeSdeSpan.RemoveAttribute("data-fact-id");
                        }
                        if (nodeListSdeSpans.Count > 0) nodeRow.SelectSingleNode("td[last()]").SetAttribute("class", "bg_blue");
                    }

                    //
                    // => Rework the table header using column spans
                    //
                    string[] columnGroups = new string[] { "financials", "tax" };
                    foreach (string columnGroup in columnGroups)
                    {
                        var nodeListSortedTableHeaderCells = xmlSortedTable.SelectNodes($"/table/thead/tr/th[@data-grouptype='{columnGroup}']");
                        var colspan = nodeListSortedTableHeaderCells.Count;
                        var counter = 0;
                        foreach (XmlNode nodeCell in nodeListSortedTableHeaderCells)
                        {
                            if (counter == 0)
                            {
                                nodeCell.SetAttribute("colspan", colspan.ToString());
                            }
                            else
                            {
                                RemoveXmlNode(nodeCell);
                            }
                            counter++;
                        }
                    }

                    //
                    // => Rework the labels in the table header section
                    //
                    var cellCounter = 0;
                    foreach (XmlNode nodeHeaderCell in xmlSortedTable.SelectNodes($"/table/thead/tr[1]/th"))
                    {
                        switch (cellCounter)
                        {
                            case 0:
                            case 1:
                                var nodeEmptyComment = xmlSortedTable.CreateComment(".");
                                nodeHeaderCell.InnerText = "";
                                nodeHeaderCell.AppendChild(nodeEmptyComment);
                                break;
                            case 3:
                                nodeHeaderCell.InnerText = "Key financials";
                                break;
                            case 5:
                                nodeHeaderCell.InnerText = "Tax contribution";
                                break;
                        }
                        cellCounter++;
                    }
                    cellCounter = 0;
                    foreach (XmlNode nodeHeaderCell in xmlSortedTable.SelectNodes($"/table/thead/tr[2]/th"))
                    {
                        switch (cellCounter)
                        {
                            case 9:
                                nodeHeaderCell.InnerText = "Corporate income tax paid";
                                break;
                                
                            case 10:
                                nodeHeaderCell.InnerText = "Customs duty";
                                break;

                            case 11:
                                nodeHeaderCell.InnerText = "VAT";
                                break;
                            case 12:
                                nodeHeaderCell.InnerText = "Payroll tax";
                                break;

                            case 14:
                                nodeHeaderCell.SetAttribute("class", "bg_blue");
                                break;
                        }
                        cellCounter++;
                    }


                    //
                    // Inject the sorted table in the content
                    //
                    var nodeSortedTableImported = xmlSection.ImportNode(xmlSortedTable.DocumentElement, true);

                    // - Inject the sorted table in place of the existing table
                    var nodeExistingTableWrapper = nodeSection.SelectSingleNode("div");
                    nodeExistingTableWrapper.ReplaceChild(nodeSortedTableImported, nodeExistingTableWrapper.SelectSingleNode("table"));


                    if (timeRoutine)
                    {
                        watch.Stop();
                        appLogger.LogInformation($"Rebuilding large Taxes Paid table: {watch.ElapsedMilliseconds} ms");
                    }
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "There was an error preparing Taxes Paid content");
            }

            return xmlSection;

        }

        /// <summary>
        /// Renders the memory cache key for retrieving and storing the content
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="siteLanguage"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        static string RenderCatCacheKey(string projectId, string articleId)
        {
            return $"philipscat_{projectId}_{articleId}";
        }

        /// <summary>
        /// Correct values for the Taxes Paid Country pages
        /// </summary>
        /// <param name="xmlSection"></param>
        /// <returns></returns>
        public static XmlDocument TaxesPaidCustomLogicHistorical(XmlDocument xmlSection, ProjectVariables projectVars, string xmlSectionFolderPathOs)
        {
            var timeRoutine = (siteType == "local");
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

            try
            {
                var nodeWrapper = xmlSection.SelectSingleNode("//div[contains(@class, 'taxes-paid-country')]");
                if (nodeWrapper != null)
                {
                    var mainTitle = xmlSection.SelectSingleNode("/data/content/article//h1")?.InnerText ?? "unknown";

                    // 
                    // => Correct PTE error value when employee value is "0"
                    //
                    var nodeSdeEmployee = nodeWrapper.SelectSingleNode("section/div/div[contains(@class, 'taxes-paid-intro')]/p[contains(text(), 'employees')]/span[@data-fact-id]");
                    if (nodeSdeEmployee != null)
                    {
                        if (nodeSdeEmployee.InnerText == "0")
                        {
                            var nodePte = nodeWrapper.SelectSingleNode("section//table[1]/tbody/tr[2]/td[4]//p[contains(text(), 'PTE')]/span[@data-fact-id and text()='err']");
                            if (nodePte != null)
                            {
                                nodePte.SetAttribute("data-syncstatus", "200-ok");
                                nodePte.SetAttribute("data-nocacheupdate", "true");
                                nodePte.InnerText = "0%";
                            }
                        }
                    }

                    //
                    // => Correct negative "0.0%" values
                    //
                    var nodeEctr = nodeWrapper.SelectSingleNode("section//table//td//p[contains(text(), 'ECTR')]/span[@data-fact-id and text()='(0.0)%']");
                    if (nodeEctr != null)
                    {
                        nodeEctr.SetAttribute("data-nocacheupdate", "true");
                        nodeEctr.InnerText = "0.0%";
                    }

                    //
                    // => Calculate the total tax paid
                    //
                    double totalTaxPaid = 0.00;


                    // - Inject the total into the span
                    var nodeTotalTaxesPaid = nodeWrapper.SelectSingleNode("section/h2[@data-labelref='totaltax']/span");
                    if (nodeTotalTaxesPaid != null)
                    {

                        // - Collect the raw data
                        var nodeListSdes = nodeWrapper.SelectNodes("section//div[@class='tp-taxes']//li[@data-labelref]/span[@data-fact-id]");
                        foreach (XmlNode nodeSde in nodeListSdes)
                        {

                            try
                            {
                                double sdeValue = 0.00;
                                var sdeValueString = nodeSde.InnerText.Trim().Replace(",", "");
                                if (sdeValueString != "" && sdeValueString != "-")
                                {
                                    if (sdeValueString.StartsWith('(') && sdeValueString.EndsWith(')')) sdeValueString = sdeValueString.Replace("(", "-").Replace(")", "");
                                    if (!Double.TryParse(sdeValueString, out sdeValue))
                                    {
                                        appLogger.LogWarning($"Unable to parse {sdeValueString} to a double (section-title: {mainTitle})");
                                    }
                                    else
                                    {
                                        totalTaxPaid += sdeValue;
                                    }
                                }
                                else
                                {
                                    appLogger.LogInformation($"No need to parse '{sdeValueString}' to a double (section-title: {mainTitle})");
                                }
                            }
                            catch (Exception ex)
                            {
                                var articleId = nodeWrapper.SelectSingleNode("ancestor::article")?.GetAttribute("id") ?? "unknown";
                                appLogger.LogError(ex, $"Unable to retrieve SDE data and calculate total for article with ID {articleId} (section-title: {mainTitle})");
                            }

                        }

                        nodeTotalTaxesPaid.InnerText = (totalTaxPaid > 0) ? totalTaxPaid.ToString("F2") : $"({totalTaxPaid.ToString("F2").Substring(1)})";
                    }
                    else
                    {
                        appLogger.LogInformation($"Unable to locate the total taxes paid value node to inject value {totalTaxPaid.ToString("F2")} in (section-title: {mainTitle})");
                    }

                    //
                    // => Fill in a non breaking space if income tax is empty
                    //
                    var nodeIncomeTax = nodeWrapper.SelectSingleNode("section//div[@class='tp-financials']//li[@data-labelref='it']/span[@data-fact-id]");
                    if (nodeIncomeTax != null)
                    {
                        if (nodeIncomeTax.InnerText.Trim() == "")
                        {
                            nodeIncomeTax.InnerXml = "&#160;";
                        }
                    }
                    var nodeCorporateIncomeTax = nodeWrapper.SelectSingleNode("section//div[@class='tp-taxes']//li[@data-labelref='cit']/span[@data-fact-id]");
                    if (nodeCorporateIncomeTax != null)
                    {
                        if (nodeCorporateIncomeTax.InnerText.Trim() == "")
                        {
                            nodeCorporateIncomeTax.InnerXml = "&#160;";
                        }
                    }


                    // // nodeCountry.SetAttribute("totaltaxpaid-formatted", totalTaxPaid.ToString("0,0.00", new CultureInfo("en-US", false)));
                    // nodeCountry.SetAttribute("totaltaxpaid-formatted", totalTaxPaid.ToString("#,##0.00;(#,##0.00)"));



                }


                //
                // => Rebuild the large Taxes Paid country pages overview table
                //
                var nodeCountryOverviewArticle = xmlSection.SelectSingleNode("/data/content/article[@id='taxes-paid-country-overview']");
                if (nodeCountryOverviewArticle != null)
                {

                    var retrieveSdeValuesFromLocalCache = true;
                    var retrieveCountryDetailPagesFromHierarchy = false;

                    if (timeRoutine) watch.Start();
                    RequestVariables reqVars = RetrieveRequestVariables(System.Web.Context.Current);



                    // Overview document in which all country details are being collected
                    var xmlTaxesPaidOverview = new XmlDocument();
                    var nodeRoot = xmlTaxesPaidOverview.CreateElement("overview");
                    var nodeGeoGraphiesRoot = xmlTaxesPaidOverview.CreateElement("geographies");
                    nodeRoot.AppendChild(nodeGeoGraphiesRoot);
                    xmlTaxesPaidOverview.AppendChild(nodeRoot);

                    //
                    // => Retrieve a list of all the coutry detail pages that we need to work with
                    //
                    var dataReferencesCountryDetailPages = new List<string>();
                    if (retrieveCountryDetailPagesFromHierarchy)
                    {
                        // Retrieve the Taxes Paid hierarchy
                        var xmlHierarchy = new XmlDocument();
                        var hierarchyPathOs = CalculateHierarchyPathOs(reqVars, projectVars.projectId, projectVars.versionId, projectVars.editorId, projectVars.editorContentType, projectVars.reportTypeId, "pdf", "taxespaid", "en");
                        try
                        {
                            xmlHierarchy.Load(hierarchyPathOs);
                        }
                        catch (Exception ex)
                        {
                            appLogger.LogError(ex, "Unable to retrieve Taxes Paid hierarchy");
                        }
                        var nodeListCountryDetailPages = xmlHierarchy.SelectNodes($"//item[@data-ref='taxes-paid-country-insights.xml']/sub_items//item[@data-ref]");
                        foreach (XmlNode nodeCountryDetailPage in nodeListCountryDetailPages)
                        {
                            dataReferencesCountryDetailPages.Add(nodeCountryDetailPage.GetAttribute("data-ref"));
                        }

                    }
                    else
                    {
                        // Retrieve the data references from the metadata XML object
                        var nodeListCountryDetailPagesFromMetadata = XmlCmsContentMetadata.SelectNodes($"/projects/cms_project[@id='{projectVars.projectId}']/data/content[metadata/entry[@key='articletype']='tp-country']");
                        foreach (XmlNode nodeMetadataContent in nodeListCountryDetailPagesFromMetadata)
                        {
                            var dataReference = nodeMetadataContent.GetAttribute("ref");
                            if (!string.IsNullOrEmpty(dataReference))
                            {
                                dataReferencesCountryDetailPages.Add(nodeMetadataContent.GetAttribute("ref"));
                            }
                            else
                            {
                                appLogger.LogWarning($"Unable to find a valid data reference (nodeMetadataContent: {nodeMetadataContent.OuterXml})");
                            }
                        }
                    }

                    // Build the cache condition
                    StringBuilder sbCondition = new StringBuilder();
                    foreach (var dataReference in dataReferencesCountryDetailPages)
                    {
                        var nodes = XmlCmsContentMetadata.SelectNodes($"/projects/cms_project[@id='{projectVars.projectId}']/data/content[@ref='{dataReference}' or @ref='__structured-data--{dataReference}']");
                        foreach (XmlNode node in nodes)
                        {
                            var contentHash = node.GetAttribute("hash");
                            if (!string.IsNullOrEmpty(contentHash)) sbCondition.Append(contentHash);
                        }
                    }
                    var cacheCondition = md5(sbCondition.ToString());

                    XmlDocument? xhtmlTable = XmlCache.Retrieve(projectVars, "tp-large-table", cacheCondition);
                    if (xhtmlTable == null)
                    {
                        //
                        // => Build a new table
                        //
                        appLogger.LogInformation("!! Build a new overview table !!");

                        // Loop through the country pages that we have found
                        foreach (var dataReference in dataReferencesCountryDetailPages)
                        {
                            var xmlCountryDetailPage = LoadAndResolveInlineFilingComposerData(reqVars, projectVars, $"{xmlSectionFolderPathOs}/{dataReference}", false);

                            if (XmlContainsError(xmlCountryDetailPage))
                            {
                                appLogger.LogError($"Could not load data. dataReference: {dataReference}, response: {xmlCountryDetailPage.OuterXml}");

                            }
                            else
                            {
                                // Retrieve the data
                                string? tpCountryName = null;
                                string? tpGeography = null;

                                var nodeCountryPageWrapper = xmlCountryDetailPage.SelectSingleNode("//div[contains(@class, 'taxes-paid-country')]");
                                if (nodeCountryPageWrapper != null)
                                {
                                    tpCountryName = nodeCountryPageWrapper.SelectSingleNode("section/h1")?.InnerText ?? null;
                                    tpGeography = nodeCountryPageWrapper.SelectSingleNode("section/h2")?.InnerText ?? null;

                                    XmlNode? nodeGeography = xmlTaxesPaidOverview.SelectSingleNode($"/overview/geographies/geography[@name='{tpGeography}']");
                                    if (nodeGeography == null)
                                    {
                                        nodeGeography = xmlTaxesPaidOverview.CreateElement("geography");
                                        nodeGeography.SetAttribute("name", tpGeography);
                                        nodeGeoGraphiesRoot.AppendChild(nodeGeography);
                                    }

                                    XmlNode nodeCountry = xmlTaxesPaidOverview.CreateElement("country");
                                    nodeCountry.SetAttribute("data-ref", dataReference);

                                    var nodeCountryName = xmlTaxesPaidOverview.CreateElementWithText("name", tpCountryName);
                                    nodeCountry.AppendChild(nodeCountryName);

                                    // 
                                    // => Append all the structured data elements
                                    //

                                    // - Employee
                                    var nodeSdeEmployee = nodeCountryPageWrapper.SelectSingleNode("section/p[@class='tp-employees']/span[@data-fact-id]");
                                    if (nodeSdeEmployee != null)
                                    {
                                        AddStructuredDataElement(nodeCountry, "empl", "Employees", nodeSdeEmployee.GetAttribute("data-fact-id"), nodeSdeEmployee.InnerText);
                                    }

                                    // - First table
                                    var nodeListCellsFirstRowFirstTable = nodeCountryPageWrapper.SelectNodes("section//div[@class='tp-financials']//li[span/@data-fact-id]");
                                    foreach (XmlNode nodeCell in nodeListCellsFirstRowFirstTable)
                                    {

                                        var labelRef = nodeCell.GetAttribute("data-labelref");

                                        var labelName = nodeCell.SelectSingleNode($"h3")?.InnerText ?? "unknown";

                                        var factId = nodeCell.SelectSingleNode("span").GetAttribute("data-fact-id");
                                        var factValue = nodeCell.SelectSingleNode("span").InnerText;
                                        // Console.WriteLine($"- labelRef: {labelRef}, factId: {factId}");

                                        AddStructuredDataElement(nodeCountry, labelRef, labelName, factId, factValue);
                                    }

                                    var nodeListCellsSecondRowFirstTable = nodeCountryPageWrapper.SelectNodes("section//div[@class='tp-financials']//li/span/span");
                                    foreach (XmlNode nodeCell in nodeListCellsSecondRowFirstTable)
                                    {
                                        AddStructuredDataElement(nodeCountry, "ectr", "ECTR", nodeCell.GetAttribute("data-fact-id"), nodeCell.InnerText);
                                    }

                                    // - Second table
                                    var nodeListCellsFirstRowSecondTable = nodeCountryPageWrapper.SelectNodes("section//div[@class='tp-taxes']//li[span/@data-fact-id]");
                                    foreach (XmlNode nodeCell in nodeListCellsFirstRowSecondTable)
                                    {
                                        var nodeTable = nodeCell.SelectSingleNode("ancestor::table");

                                        var labelRef = nodeCell.GetAttribute("data-labelref");

                                        var labelName = nodeCell.SelectSingleNode($"h3")?.InnerText ?? "unknown";

                                        var factId = nodeCell.SelectSingleNode("span").GetAttribute("data-fact-id");
                                        var factValue = nodeCell.SelectSingleNode("span").InnerText;
                                        // Console.WriteLine($"- labelRef: {labelRef}, factId: {factId}");

                                        AddStructuredDataElement(nodeCountry, labelRef, labelName, factId, factValue);
                                    }

                                    var nodeListCellsSecondRowSecondTable = nodeCountryPageWrapper.SelectNodes("section//div[@class='tp-taxes']//li/span/span");
                                    foreach (XmlNode nodeCell in nodeListCellsSecondRowSecondTable)
                                    {

                                        var labelRef = (nodeCell.ParentNode.HasAttribute("data-ctr")) ? "ctr" : "pte";
                                        var labelName = (nodeCell.ParentNode.HasAttribute("data-ctr")) ? "CTR" : "PTE";

                                        AddStructuredDataElement(nodeCountry, labelRef, labelName, nodeCell.GetAttribute("data-fact-id"), nodeCell.InnerText);
                                    }


                                    nodeGeography.AppendChild(nodeCountry);


                                }
                                else
                                {
                                    appLogger.LogError($"Unable to locate the taxes paid wrapper div in {dataReference}");
                                }

                                appLogger.LogInformation($"* dataReference: {dataReference}, tpCountryName: {tpCountryName}, tpGeography: {tpGeography}");


                            }
                        }


                        //
                        // => Retrieve all the fact ID's used in the country detail pages
                        //
                        if (!retrieveSdeValuesFromLocalCache)
                        {
                            var factIds = new List<string>();
                            var nodeListSdes = xmlTaxesPaidOverview.SelectNodes("/overview/geographies/geography/country/span[@data-fact-id]");
                            foreach (XmlNode nodeSde in nodeListSdes)
                            {
                                var factId = nodeSde.GetAttribute("data-fact-id");
                                if (!factIds.Contains(factId))
                                {
                                    factIds.Add(factId);
                                }
                                else
                                {
                                    appLogger.LogError($"Fact ID {factId} is not unique!");
                                }
                            }

                            //
                            // => Retrieve the SDE values
                            //            
                            Console.WriteLine("** Retrieve Taxes Paid SDE values **");
                            var sdeValuesResult = RetieveStructuredDataValue(string.Join(",", factIds), projectVars.projectId, null, false).GetAwaiter().GetResult();

                            if (sdeValuesResult.Success)
                            {
                                var xmlSdeValues = sdeValuesResult.XmlPayload;

                                // Inject in result
                                foreach (XmlNode nodeSde in nodeListSdes)
                                {
                                    var factId = nodeSde.GetAttribute("data-fact-id");

                                    var nodeSdeValue = xmlSdeValues.SelectSingleNode($"/response/item[@id='{factId}']/value[@lang='{projectVars.outputChannelVariantLanguage}']");
                                    if (nodeSdeValue != null)
                                    {
                                        nodeSde.InnerText = nodeSdeValue.InnerText;
                                    }
                                    else
                                    {
                                        appLogger.LogError($"Could not retrieve SDE value for factId {factId} - fact id not present in the result");
                                    }

                                }
                            }
                        }
                        else
                        {
                            xmlTaxesPaidOverview.ReplaceContent(SyncStructuredDataElements(xmlTaxesPaidOverview, $"{xmlSectionFolderPathOs}/taxes-paid-country-overview.xml", projectVars.projectId), true);
                        }

                        //
                        // => Calculate the sum of the total taxes that were paid
                        //
                        string[] labelReferences = { "cit", "cd", "vat", "pt", "ot" };
                        var nodeListCountries = xmlTaxesPaidOverview.SelectNodes("/overview/geographies/geography/country");
                        foreach (XmlNode nodeCountry in nodeListCountries)
                        {
                            var countryName = nodeCountry.SelectSingleNode("name")?.InnerText ?? "unknown";
                            if (countryName == "Costa Rica")
                            {
                                appLogger.LogDebug("in costa rica");
                            }

                            double totalTaxPaid = 0.00;
                            bool dashFound = false;

                            // - Collect the raw data
                            foreach (var labelReference in labelReferences)
                            {

                                try
                                {
                                    double sdeValue = 0.00;
                                    var nodeSpan = nodeCountry.SelectSingleNode($"span[@label='{labelReference}']");
                                    var sdeValueString = nodeSpan.InnerText.Trim().Replace(",", "");
                                    if (sdeValueString != "" && sdeValueString != "-")
                                    {
                                        if (sdeValueString.StartsWith('(') && sdeValueString.EndsWith(')')) sdeValueString = sdeValueString.Replace("(", "-").Replace(")", "");
                                        if (!Double.TryParse(sdeValueString, out sdeValue))
                                        {
                                            appLogger.LogWarning($"Unable to parse {sdeValueString} to a double (countryName: {countryName}, factName: '{nodeSpan.GetAttribute("name") ?? "Unknown"}')");
                                        }
                                        else
                                        {
                                            totalTaxPaid += sdeValue;
                                        }
                                    }
                                    else
                                    {
                                        if (sdeValueString == "-") dashFound = true;
                                        appLogger.LogInformation($"No need to parse '{sdeValueString}' to a double (countryName: {countryName}, factName: '{nodeSpan.GetAttribute("name") ?? "Unknown"}')");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    appLogger.LogError(ex, $"Unable to retrieve SDE data and calculate total for {nodeCountry.GetAttribute("data-ref")}, labelReference: {labelReference}");
                                }

                            }

                            nodeCountry.SetAttribute("totaltaxpaid", totalTaxPaid.ToString("F2"));

                            // nodeCountry.SetAttribute("totaltaxpaid-formatted", totalTaxPaid.ToString("0,0.00", new CultureInfo("en-US", false)));

                            // Correctly format 0.00 value and insert it
                            var totalTaxPaidFormatted = totalTaxPaid.ToString("#,##0.00;(#,##0.00)");
                            if (totalTaxPaidFormatted == "0.00") totalTaxPaidFormatted = (dashFound) ? "-" : "";
                            nodeCountry.SetAttribute("totaltaxpaid-formatted", totalTaxPaidFormatted);
                        }

                        // Store the result of the data collection process on the disk
                        xmlTaxesPaidOverview.SaveAsync($"{logRootPathOs}/taxespaidoverview.xml", true, true).GetAwaiter().GetResult();

                        //
                        // => Render the HTML of the table
                        //
                        xhtmlTable = TransformXmlToDocument(xmlTaxesPaidOverview, $"{applicationRootPathOs}/backend/code/custom/taxespaid-overviewtable.xsl");

                        //
                        // => Push the table in the cache
                        //
                        XmlCache.Set(projectVars, "tp-large-table", cacheCondition, xhtmlTable);


                    }
                    else
                    {
                        appLogger.LogInformation(":-) Using cache :-)");
                    }






                    //
                    // => Replace the current large overview table with the one we just created
                    //
                    var nodeTableToReplace = nodeCountryOverviewArticle.SelectSingleNode("*//table");
                    if (nodeTableToReplace != null)
                    {

                        ReplaceXmlNode(nodeTableToReplace, xhtmlTable.DocumentElement);


                        if (timeRoutine)
                        {
                            watch.Stop();
                            appLogger.LogInformation($"Rebuilding large Taxes Paid table: {watch.ElapsedMilliseconds} ms");
                        }
                    }

                    /// <summary>
                    /// Helper function that adds an HTML representation of a SDE
                    /// </summary>
                    /// <param name="nodeCountry"></param>
                    /// <param name="label"></param>
                    /// <param name="name"></param>
                    /// <param name="factId"></param>
                    void AddStructuredDataElement(XmlNode nodeCountry, string label, string name, string factId, string factValue)
                    {
                        var nodeSde = xmlTaxesPaidOverview.CreateElement("span");
                        nodeSde.SetAttribute("label", label);
                        nodeSde.SetAttribute("name", name);
                        nodeSde.SetAttribute("data-fact-id", factId);
                        nodeSde.InnerText = factValue;
                        nodeCountry.AppendChild(nodeSde);
                    }
                }
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "There was an error preparing Taxes Paid content");
            }

            //
            // => Return the modified section content
            //
            return xmlSection;
        }

        /// <summary>
        /// Philips unique post process logic on-section-translation set
        /// </summary>
        /// <param name="nodeArticle"></param>
        /// <param name="projectVars"></param>
        /// <returns></returns>
        public static XmlNode PhilipsPostProcessLangClone(XmlNode nodeArticle, ProjectVariables projectVars)
        {
            var nodeListToRemove = nodeArticle.SelectNodes("//*[contains(@class, 'data20-f')]");
            if (nodeListToRemove.Count > 0)
            {
                RemoveXmlNodes(nodeListToRemove);
                appLogger.LogInformation($"Removing {nodeListToRemove.Count} 20-F only elements");
            }

            return nodeArticle;
        }


    }
}