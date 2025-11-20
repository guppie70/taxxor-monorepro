<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    <xsl:param name="inline-css"/>
    <xsl:param name="inline-js"/>
    <xsl:param name="mode">graphs</xsl:param>
    <xsl:param name="baseurl"/>
    <xsl:param name="rootfolder"/>
    <xsl:param name="token"/>
    <xsl:param name="graphengine">echarts</xsl:param>
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
                <title>Graph generator</title>

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

                <link rel="stylesheet" type="text/css" href="{$baseurl}/stylesheets/render-graphs/graphs.css?token={$token}&amp;internal=true"/>
                <xsl:comment>These are styles maintained in the Taxxor Editor and injected here in the head section of the HTML document</xsl:comment>
                <xsl:if test="string-length(normalize-space($inline-css)) > 0">
                    <style>
                    <xsl:value-of select="$inline-css"/>
                </style>
                </xsl:if>

                <!-- Javascript files -->
                <xsl:for-each select="$clientassets//js/file">
                    <script type="text/javascript">
                        <xsl:attribute name="src">
                            <xsl:value-of select="$baseurl"/>
                            <xsl:call-template name="add-access-token">
                                <xsl:with-param name="uri" select="@uri"/>
                            </xsl:call-template>
                        </xsl:attribute>
                        <xsl:text>//</xsl:text>
                    </script>
                </xsl:for-each>
                
                <xsl:if test="$graphengine = 'highcharts'">
                    <script type="text/javascript" src="{$baseurl}{$rootfolder}/js/pdf.js?token={$token}&amp;internal=true">//</script>
                </xsl:if>
                
                <script type="text/javascript" src="{$baseurl}/javascript/render-graphs/scripts.js?token={$token}&amp;internal=true">//</script>

                <xsl:comment>These are scripts maintained in the Taxxor Editor and injected here in the head section of the HTML document</xsl:comment>
                <xsl:if test="string-length(normalize-space($inline-js)) > 0">
                    <script type="text/javascript">
                        <xsl:value-of select="$inline-js" disable-output-escaping="yes"/>
                    </script>
                </xsl:if>
                
                <script type="text/javascript">
                    var graphEngine='<xsl:value-of select="$graphengine"/>';
                </script>

            </head>
            <body style="background-color: #fff" id="tx-renderchartsutility">

                <div class="controls">
                    <table width="100%">
                        <tr class="controls">
                            <td colspan="2">
                                <!-- Generic controls -->
                                <input id="chk-display-headerfooter" type="checkbox" name="vehicle" onclick="toggleHeaderFooter(this)" checked="checked"/> Show header and footer<br/>
                                <input id="chk-display-datatables" type="checkbox" name="vehicle" onclick="toggleDataTables(this)"/> Show data tables<br/>
                            </td>
                        </tr>
                        <tr class="selector">
                            <td class="col1">
                                <b>Graph / Drawing selector</b>
                                <br/>
                                <!-- Select a graph -->
                                <input type="radio" name="showgraph" value="all" checked="checked" onclick="showHideSingleChart('all', true);"/> All<br/>
                                <xsl:for-each select="//article//section//div[(div/@class='chart-content' or div/@class='tx-renderedchart') and table]">
                                    <input type="radio" name="showgraph" value="{@id}" onclick="showHideSingleChart('{@id}', true);"/>
                                    <xsl:value-of select="./div[@class = 'tablegraph-header-wrapper']//p[contains(@class, 'maintitle')]/text()"/><br/>
                                </xsl:for-each>
                                <hr/>
                                <!-- Select a SVG drawing -->
                                <input type="radio" name="showsvgdrawing" value="all" checked="checked" onclick="showHideSingleSvgDrawing('all', true);"/> All<br/>
                                <xsl:for-each select="//article//section//div[@class = 'illustration' and @data-assetnameconvert]">
                                    <input type="radio" name="showsvgdrawing" value="{@id}" onclick="showHideSingleSvgDrawing('{@id}', true);"/>
                                    <xsl:value-of select="@id"/><br/>
                                </xsl:for-each>
                            </td>
                            <td class="col2">
                                <b>Documentation: Commands in conversion service</b>
                                <p>Located in: /javascript/render-graphs/scripts.js</p>
                                <ul class="code">
                                    <li><a href="javascript:showHideHeaderFooter(false)">showHideHeaderFooter(false)</a></li>
                                    <li><a href="javascript:showHideControls(false)">showHideControls(false)</a></li>
                                    <li><a href="javascript:showHideAllCharts(true)">showHideAllCharts(true)</a></li>
                                    <li><a href="javascript:getChartsInfoDisplay()">getChartsInfo()</a></li>
                                    <li><a href="javascript:retrieveAllRenderdedChartsDisplay()">retrieveAllRenderdedCharts()</a></li>
                                    <li><a href="javascript:showHideAllCharts(false)">showHideAllCharts(false)</a></li>
                                    <li>Then the conversion service uses showHideSingleChart('${chartId}', true) to render each graph and take a screenshot</li>
                                </ul>
                            </td>
                        </tr>
                        <tr class="debug">
                            <td colspan="2">
                                <div id="tx-debugoutput">...</div>
                            </td>
                        </tr>
                    </table>

                </div>

                <div id="graph-wrapper">
                    <xsl:apply-templates select="//article//section//div[(div/@class='chart-content' or div/@class='tx-renderedchart') and table]"/>
                </div>


            </body>
        </html>
    </xsl:template>

    <xsl:template match="div[@class = 'chart-content']">
        <div style="border: 0">
            <xsl:copy-of select="@*"/>
            <xsl:comment>.</xsl:comment>
        </div>
    </xsl:template>
    
    <xsl:template match="div[@class = 'tx-renderedchart']">
        <div style="border: 0">
            <xsl:copy-of select="@*"/>
            <xsl:comment>.</xsl:comment>
        </div>
    </xsl:template>

    <xsl:template match="img"/>

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
