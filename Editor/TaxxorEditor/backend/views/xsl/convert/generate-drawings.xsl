<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    <xsl:param name="inline-css"/>
    <xsl:param name="inline-js"/>
    <xsl:param name="mode">graphs</xsl:param>
    <xsl:param name="baseurl"/>
    <xsl:param name="rootfolder"/>
    <xsl:param name="token"/>
    <xsl:param name="addhardcodedvisuals">true</xsl:param>
    <xsl:param name="clientassets"/>
    <xsl:param name="lang">en</xsl:param>

    <xsl:output method="xml" omit-xml-declaration="yes" indent="yes" encoding="UTF-8"/>

    <xsl:template match="@* | * | processing-instruction()">
        <xsl:copy>
            <xsl:apply-templates select="* | @* | text() | processing-instruction()"/>
        </xsl:copy>
    </xsl:template>


    <xsl:template match="/">
        <html lang="{$lang}">
            <head>
                <meta charset="UTF-8"/>
                <title>Visuals generator</title>

                <!-- CSS stylesheets -->
                <xsl:for-each select="$clientassets//css/file">
                    <link rel="stylesheet" type="text/css">
                        <xsl:attribute name="href">
                            <xsl:value-of select="$baseurl"/>
                            <xsl:call-template name="add-access-token">
                                <xsl:with-param name="uri" select="@uri"/>
                            </xsl:call-template>
                        </xsl:attribute>
                    </link>
                </xsl:for-each>
                
                <link rel="stylesheet" type="text/css" href="{$baseurl}/stylesheets/render-drawings/base.css?token={$token}&amp;internal=true"/>
                <xsl:comment>These are styles maintained in the Taxxor Editor and injected here in the head section of the HTML document</xsl:comment>
                <xsl:if test="string-length(normalize-space($inline-css)) > 0">
                    <style>
                    <xsl:value-of select="$inline-css"/>
                </style>
                </xsl:if>


                <!-- Javascript files -->
                <xsl:for-each select="$clientassets//js/file">
                    <script>
                        <xsl:attribute name="src">
                            <xsl:value-of select="$baseurl"/>
                            <xsl:call-template name="add-access-token">
                                <xsl:with-param name="uri" select="@uri"/>
                            </xsl:call-template>
                        </xsl:attribute>
                        <xsl:text>//</xsl:text>
                    </script>
                </xsl:for-each>
                
                
                <script type="text/javascript" src="{$baseurl}/scripts/jquery.js?token={$token}&amp;internal=true">//</script>
                <script type="text/javascript" src="{$baseurl}/javascript/render-drawings/scripts.js?token={$token}&amp;internal=true">//</script>

                <xsl:comment>These are scripts maintained in the Taxxor Editor and injected here in the head section of the HTML document</xsl:comment>
                <xsl:if test="string-length(normalize-space($inline-js)) > 0">
                    <script type="text/javascript">
                        <xsl:value-of select="$inline-js" disable-output-escaping="yes"/>
                    </script>
                </xsl:if>

            </head>
            <body style="background-color: #fff">
                <xsl:comment>
                    baseurl: <xsl:value-of select="$baseurl"/>
                </xsl:comment>

                <div class="controls">
                    <table width="100%">
                        <tr>
                            <td class="col1">
                                <!-- Generic controls -->
                                <input id="chk-display-headerfooter" type="checkbox" name="vehicle" onclick="toggleHeaderFooter(this)" checked="checked"/> Show header and footer<br/>
                                <input id="chk-display-datatables" type="checkbox" name="vehicle" onclick="toggleDataTables(this)"/> Show data tables<br/>
                                <!--
                                <hr/>
                                <input id="chk-display-datatables" type="checkbox" name="vehicle" onclick="toggleSvgDrawings(this)"/> Show data tables<br/>
                                -->
                                <hr/>
                                <b>Commands in conversion service</b>
                                <p>From: /javascript/render-graphs/scripts.js</p>
                                <p>Phase 1 in conversion service: retrieveDrawingInformation()</p>
                                <ul>
                                    <li><a href="javascript:drawingsLoadedStatistics()">drawingsLoadedStatistics()</a> - called multiple times to assure that SVG's have been loaded</li>
                                    <li><a href="javascript:showHideHeaderFooter(false)">showHideHeaderFooter(false)</a></li>
                                    <li><a href="javascript:showHideControls(false)">showHideControls(false)</a></li>
                                    <li><a href="javascript:showHideAllSvgDrawings(true)">showHideAllSvgDrawings(true)</a></li>
                                    <li><a href="javascript:getSvgDrawingInfoDisplay()">getSvgDrawingInfo()</a></li>
                                </ul>
                                <p>Phase 2 in the conversion service: convert()</p>
                                <ul>
                                    <li><a href="javascript:showHideHeaderFooter(false)">showHideHeaderFooter(false)</a></li>
                                    <li><a href="javascript:showHideControls(false)">showHideControls(false)</a></li>
                                    <li>Then the conversion service loops through all the SVG files and hides using hideSingleSvgDrawing('${drawingId}') after a screenshot has been taken</li>
                                </ul>
                                <div id="tx-debugoutput">...</div>
                            </td>
                            <td class="col2">
                                <!-- Select a SVG drawing -->
                                <input type="radio" name="showsvgdrawing" value="all" checked="checked" onclick="showHideSingleSvgDrawing('all', true);"/> All<br/>
                                <xsl:if test="$addhardcodedvisuals = 'true'">
                                    <input type="radio" name="showsvgdrawing" value="{@id}" onclick="showHideSingleSvgDrawing('value-creation', true);"/>
                                    <xsl:text> value creation - </xsl:text>
                                    <a href="javascript:hideSingleSvgDrawing('value-creation');">hide</a>
                                    <br/>
                                </xsl:if>
                                <xsl:for-each select="//div[@class = 'illustration' and (object or img)]">
                                    <input type="radio" name="showsvgdrawing" value="{@id}" onclick="showHideSingleSvgDrawing('{@id}', true);"/>
                                    <xsl:value-of select="@id"/> - <a href="javascript:hideSingleSvgDrawing('{@id}');">hide</a><br/>
                                </xsl:for-each>
                            </td>
                        </tr>
                    </table>

                </div>

                <div id="svg-wrapper">
                    <!-- Hard coded... -->
                    <xsl:if test="$addhardcodedvisuals = 'true'">
                        <div class="illustration" component="value-creation" id="value-creation" data-assetnameconvert="visualdrawing0000.png" data-assetnameuse="visualdrawing0000.jpg">
                            <object type="image/svg+xml" data="{$baseurl}/dataserviceassets/ar19/images/svg/value-creation.svg?token={$token}&amp;internal=true"> </object>
                        </div>
                    </xsl:if>
                    <xsl:apply-templates select="//div[contains(@class, 'illustration') and (object or img)]"/>
                </div>
                
            </body>
        </html>
    </xsl:template>

    <xsl:template match="object">
        <object type="image/svg+xml">
            <xsl:attribute name="data">
                <xsl:value-of select="concat($baseurl, @data, '?token=', $token, '&amp;internal=true')"/>
            </xsl:attribute>
        </object>
    </xsl:template>

    <xsl:template match="img"/>
    <xsl:template match="img[contains(@src, '.svg')]">
        <img>
            <xsl:attribute name="src">
                <xsl:value-of select="concat($baseurl, @data, '?token=', $token, '&amp;internal=true')"/>
            </xsl:attribute>
        </img>
    </xsl:template>
    
    
    <xsl:template name="add-access-token">
        <xsl:param name="uri"/>
        <xsl:choose>
            <xsl:when test="contains($uri, '?')">
                <xsl:value-of select="concat($uri, '&amp;token=', $token, '&amp;internal=true')"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="concat($uri, '?token=', $token, '&amp;internal=true')"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

</xsl:stylesheet>
