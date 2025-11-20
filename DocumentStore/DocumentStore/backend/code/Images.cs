using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Taxxor.Project
{

    /// <summary>
    /// Logic for dealing with internal links
    /// </summary>
    public abstract partial class ProjectLogic : Framework
    {

        /// <summary>
        /// Loops through the images in a section and processes them
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="context"></param>
        /// <param name="xmlHierarchy"></param>
        /// <returns></returns>
        public static XmlDocument ProcessImagesOnLoad(XmlDocument xmlDocument, RequestVariables reqVars, ProjectVariables projectVars, string xmlSectionPathOs)
        {

            try
            {
                //
                // => Wrap the image in an HTML construction with a caption
                //
                var useClassicMarkup = true;
                var wrapperNodeName = (useClassicMarkup) ? "span" : "figure";
                var captionNodeName = (useClassicMarkup) ? "span" : "figcaption";


                var nodeListImagesWithCaption = xmlDocument.SelectNodes($"/data/content[@lang='{projectVars.outputChannelVariantLanguage}']/*//img[@data-rendercaption='true']");
                // Console.WriteLine($"Found {nodeListImagesWithCaption.Count} images with captions");
                foreach (XmlNode nodeImageWithCaption in nodeListImagesWithCaption)
                {
                    // Classic HTML structure
                    var nodeImageWrapper = xmlDocument.CreateElement(wrapperNodeName);
                    nodeImageWrapper.SetAttribute("class", "image");
                    nodeImageWrapper.SetAttribute("data-wrapperdynamicallyrendered", "true");
                    var nodeImageWithCaptionCloned = nodeImageWithCaption.CloneNode(true);
                    nodeImageWrapper.AppendChild(nodeImageWithCaptionCloned);
                    var nodeCaption = xmlDocument.CreateElement(captionNodeName);
                    nodeCaption.SetAttribute("class", "caption");
                    nodeCaption.SetAttribute("contenteditable", "false");
                    nodeCaption.SetAttribute("data-contenteditable", "false");
                    nodeCaption.InnerText = nodeImageWithCaptionCloned.GetAttribute("alt") ?? "";
                    nodeImageWrapper.AppendChild(nodeCaption);

                    nodeImageWithCaption.ParentNode.ReplaceChild(nodeImageWrapper, nodeImageWithCaption);
                }

            }
            catch (Exception ex)
            {
                // Make sure that we log the issue
                appLogger.LogError(ex, "There was a problem processing the images on load");

                // Rethrow the error, to make sure that the loading logic stops
                throw;
            }

            return xmlDocument;
        }

        /// <summary>
        /// Restores the original markup of the images in a section
        /// </summary>
        /// <param name="xmlDocument"></param>
        /// <param name="reqVars"></param>
        /// <param name="projectVars"></param>
        /// <param name="xmlSectionPathOs"></param>
        /// <returns></returns>
        public static XmlDocument ProcessImagesOnSave(XmlDocument xmlDocument, ProjectVariables projectVars)
        {

            try
            {
                var nodeListImageWrappersToBeProcessed = xmlDocument.SelectNodes($"/data/content[@lang='{projectVars.outputChannelVariantLanguage}']//*[@data-wrapperdynamicallyrendered='true']");
                foreach (XmlNode nodeImageWrapper in nodeListImageWrappersToBeProcessed)
                {
                    var nodeImage = nodeImageWrapper.SelectSingleNode("img");
                    if (nodeImage == null)
                    {
                        nodeImageWrapper.ParentNode.RemoveChild(nodeImageWrapper);
                    }
                    else
                    {
                        nodeImageWrapper.ParentNode.ReplaceChild(nodeImage.CloneNode(true), nodeImageWrapper);
                    }
                }

            }
            catch (Exception ex)
            {
                // Make sure that we log the issue
                appLogger.LogError(ex, "There was a problem processing the images on save");

                // Rethrow the error, to make sure that the loading logic stops
                throw;
            }

            return xmlDocument;
        }



    }


}