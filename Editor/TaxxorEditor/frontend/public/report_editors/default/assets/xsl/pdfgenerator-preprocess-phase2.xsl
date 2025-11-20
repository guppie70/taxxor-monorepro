<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    <xsl:param name="pageId"/>
    <xsl:param name="editorId"/>
    <xsl:param name="reportTypeId"/>
    <xsl:param name="guidLegalEntity"/>
    <xsl:param name="variantId"/>
    <xsl:param name="layout">regular</xsl:param>
    <xsl:param name="date"/>
    <xsl:param name="projectRootPath"/>
    <xsl:param name="taxxorClientId"/>
    <xsl:param name="includePdfCssStylesheet">true</xsl:param>
    <xsl:param name="renderScope"/>
    <xsl:param name="sections"/>
    <xsl:param name="generatedformat">pdf</xsl:param>
    <xsl:param name="baseurl"/>
    <xsl:param name="token"/>
    <xsl:param name="printready">no</xsl:param>
    <xsl:param name="tablesonly">no</xsl:param>
    <xsl:param name="clientassets"/>
    <xsl:param name="appversion"/>
    <xsl:param name="signature-marks">true</xsl:param>
    <xsl:param name="hideerrors">no</xsl:param>
    <xsl:param name="usecontentstatus">no</xsl:param>
    <xsl:param name="disableexternallinks">no</xsl:param>
    <xsl:param name="mode">normal</xsl:param>
    <xsl:param name="reportcaption"></xsl:param>
    <xsl:param name="lang">unknown</xsl:param>
    

    <xsl:output encoding="UTF-8" method="xml" omit-xml-declaration="yes" cdata-section-elements="style"/>

    <xsl:template match="@* | * | processing-instruction() | comment()">
        <xsl:copy>
            <xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
        </xsl:copy>
    </xsl:template>

    <xsl:template match="/">
        <xsl:choose>
            <xsl:when test="$generatedformat = 'msword' or $generatedformat = 'xbrl'">
                <xsl:apply-templates/>
            </xsl:when>
            <xsl:when test="$mode = 'nochrome'">
                <body data-editorid="{$editorId}" data-reportypeid="{$reportTypeId}" data-guidlegalentity="{$guidLegalEntity}" data-variantid="{$variantId}" data-renderscope="{$renderScope}" data-sections="{$sections}" data-generatedformat="{$generatedformat}" data-printready="{$printready}" data-tablesonly="{$tablesonly}" data-layout="{$layout}" data-reportcaption="{$reportcaption}" data-usecontentstatus="{$usecontentstatus}">
                    <xsl:attribute name="class">
                        <xsl:if test="$signature-marks = 'no' or $signature-marks = 'false'">
                            <xsl:text>hide-signature-marks</xsl:text>
                        </xsl:if>
                    </xsl:attribute>

                    <!-- Copy the complete content of the XHTML that we have received to the output stream -->
                    <xsl:choose>
                        <xsl:when test="$generatedformat = 'pdf'">
                            <content>
                                <xsl:apply-templates/>
                            </content>
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:apply-templates/>
                        </xsl:otherwise>
                    </xsl:choose>

                </body>
            </xsl:when>
            <xsl:otherwise>
                <xsl:variable name="css-style-suffix">
                    <xsl:value-of select="concat($variantId, '_', $layout)"/>
                </xsl:variable>
                <html lang="{$lang}">
                    <xsl:attribute name="class">
                        <xsl:choose>
                            <xsl:when test="$hideerrors = 'yes'">
                                <xsl:text>hide-errors</xsl:text>
                            </xsl:when>
                            <xsl:otherwise>
                                 <xsl:text>show-errors</xsl:text>
                            </xsl:otherwise>
                        </xsl:choose>
                    </xsl:attribute>
                    
                    <head>
                        <meta charset="utf-8"/>
                        <meta http-equiv="X-UA-Compatible" content="IE=edge"/>
                        <meta name="viewport" content="width=device-width, initial-scale=1"/>
                        <meta name="date" content="{$date}"/>

                        <title>PDF HTML</title>
                        
                        <!--
                        <xsl:comment>
                            * taxxorClientId: <xsl:value-of select="$taxxorClientId"/>
                            * guidLegalEntity: <xsl:value-of select="$guidLegalEntity"/>
                        </xsl:comment>
                        -->
                        
                        <!-- For page, column and section breaks -->
                        <style type="text/css">
                            .tx-pb-b_<xsl:value-of select="$css-style-suffix"/> { break-before: page; }  
                            .tx-cb-b_<xsl:value-of select="$css-style-suffix"/> { break-before: column; }
                            .tx-flt-b_<xsl:value-of select="$css-style-suffix"/> { -prince-float: page bottom; }
                            .tx-flt-t_<xsl:value-of select="$css-style-suffix"/> { -prince-float: page top; }
                            .tx-cs-a_<xsl:value-of select="$css-style-suffix"/> { column-span: all; }
                        </style>

                        <!-- Stylesheet that contains default styles for track changes, SDE warnings, etc. -->
                        <link rel="stylesheet" type="text/css" href="/outputchannels/stylesheets/statusindicators.css"/>

                        <!-- CSS stylesheets -->
                        <xsl:for-each select="$clientassets//css/file">
                            <link rel="stylesheet" type="text/css" href="{@uri}"/>
                        </xsl:for-each>

                        <!-- Javascript files -->
                        <xsl:for-each select="$clientassets//js/file">
                            <script src="{@uri}">//</script>
                        </xsl:for-each>
                        
                    </head>
                    <body data-editorid="{$editorId}" data-reportypeid="{$reportTypeId}" data-guidlegalentity="{$guidLegalEntity}" data-variantid="{$variantId}" data-renderscope="{$renderScope}" data-sections="{$sections}" data-generatedformat="{$generatedformat}" data-printready="{$printready}" data-tablesonly="{$tablesonly}" data-layout="{$layout}" data-reportcaption="{$reportcaption}" data-usecontentstatus="{$usecontentstatus}">
                        <xsl:attribute name="class">
                            <xsl:if test="$signature-marks = 'no' or $signature-marks = 'false'">
                                <xsl:text>hide-signature-marks</xsl:text>
                            </xsl:if>
                        </xsl:attribute>

                        <!-- Copy the complete content of the XHTML that we have received to the output stream -->
                        <xsl:choose>
                            <xsl:when test="$generatedformat = 'pdf'">
                                <content>
                                    <xsl:apply-templates/>
                                </content>
                            </xsl:when>
                            <xsl:otherwise>
                                <xsl:apply-templates/>
                            </xsl:otherwise>
                        </xsl:choose>

                    </body>
                </html>
            </xsl:otherwise>
        </xsl:choose>



    </xsl:template>
    
    
    <xsl:template match="body">
        <body data-editorid="{$editorId}" data-reportypeid="{$reportTypeId}" data-guidlegalentity="{$guidLegalEntity}" data-variantid="{$variantId}" data-renderscope="{$renderScope}" data-sections="{$sections}" data-generatedformat="{$generatedformat}" data-printready="{$printready}" data-tablesonly="{$tablesonly}" data-layout="{$layout}" data-reportcaption="{$reportcaption}" data-usecontentstatus="{$usecontentstatus}">
            <xsl:copy-of select="@*"/>
            <xsl:apply-templates/>
        </body>
    </xsl:template>
    
    
    <xsl:template match="style">
        <style type="text/css">
            <xsl:choose>
                <xsl:when test="$generatedformat = 'xbrl'">
                    <xsl:text disable-output-escaping="yes">&lt;![CDATA[</xsl:text>
                    <xsl:value-of select="." disable-output-escaping="yes"/>
                    <xsl:text disable-output-escaping="yes">]]&gt;</xsl:text>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:value-of select="."/>
                </xsl:otherwise>
            </xsl:choose>
        </style>
    </xsl:template>

    <xsl:template match="img">
        <xsl:choose>
            <xsl:when test="$generatedformat = 'pdf' or $generatedformat = 'xbrl' or $baseurl = '' or $token = ''">
                <img>
                    <xsl:copy-of select="@*"/>
                </img>
            </xsl:when>
            <xsl:when test="@data-contentencoding='base64'">
                <img>
                    <xsl:copy-of select="@*"/>
                </img>
            </xsl:when>
            <xsl:otherwise>
                <img>
                    <xsl:copy-of select="@*[not(local-name(.) = 'src')]"/>
                    <xsl:attribute name="src">
                        <xsl:choose>
                            <xsl:when test="contains(@src, '?')">
                                <xsl:value-of select="concat($baseurl, @src, '&amp;token=', $token)"/>
                            </xsl:when>
                            <xsl:otherwise>
                                <xsl:value-of select="concat($baseurl, @src, '?token=', $token)"/>
                            </xsl:otherwise>
                        </xsl:choose>
                    </xsl:attribute>
                </img>
            </xsl:otherwise>
        </xsl:choose>


    </xsl:template>

    <xsl:template match="object">
        <xsl:choose>
            <xsl:when test="$generatedformat = 'pdf' or $generatedformat = 'xbrl' or $baseurl = '' or $token = ''">
                <object>
                    <xsl:copy-of select="@*"/>
                </object>
            </xsl:when>
            <xsl:otherwise>
                <object>
                    <xsl:copy-of select="@*[not(local-name(.) = 'data')]"/>
                    <xsl:attribute name="data">
                        <xsl:choose>
                            <xsl:when test="contains(@data, '?')">
                                <xsl:value-of select="concat($baseurl, @data, '&amp;token=', $token)"/>
                            </xsl:when>
                            <xsl:otherwise>
                                <xsl:value-of select="concat($baseurl, @data, '?token=', $token)"/>
                            </xsl:otherwise>
                        </xsl:choose>
                    </xsl:attribute>
                </object>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <xsl:template match="small[@class = 'edit-xbrl-concept']"/>

    <xsl:template match="div[contains(@class, 'xbrl-level-2')]">
        <xsl:choose>
            <xsl:when test="$generatedformat = 'xbrl'">
                <div>
                    <xsl:copy-of select="@*"/>
                    <xsl:apply-templates/>
                </div>
            </xsl:when>
            <xsl:otherwise>
                <xsl:apply-templates/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <!-- make sure that breaks do not have any styling on them -->
    <xsl:template match="br[@style]">
        <br/>
    </xsl:template>
    
    <xsl:template match="a">
        <xsl:choose>
            <xsl:when test="$generatedformat = 'msword'">
                <!-- Remove all the links from an msword document -->
                <xsl:apply-templates/>
            </xsl:when>
            <xsl:when test="@data-link-type = 'external' and $disableexternallinks = 'yes'">
                <!-- Remove links to external websites when we generate an output channel that explicitly indicated we should not render these -->
                <xsl:apply-templates/>
            </xsl:when>
            <xsl:when test="(starts-with(@href, 'http') or starts-with(@href, 'mailto:') or starts-with(@href, 'javascript:') or contains(@href, 'www.')) and $disableexternallinks = 'yes'">
                <!-- Remove links to external websites when we generate an output channel that explicitly indicated we should not render these -->
                <xsl:apply-templates/>
            </xsl:when>
            <xsl:otherwise>
                <a>
                    <xsl:copy-of select="@*"/>
                    <xsl:apply-templates/>
                </a>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>
    
    <!-- strip the metadata element -->
    <xsl:template match="metadata"/>

    <!-- Strip any content elements -->
    <xsl:template match="content">
        <xsl:apply-templates/>
    </xsl:template>
    
    <!-- Table footnote elements need to be block-level for Word -->
    <xsl:template match="div[@class='tablegraph-footer-wrapper']/span[@data-footnoteid]">
        <xsl:choose>
            <xsl:when test="$generatedformat = 'msword'">
                <div>
                    <xsl:copy-of select="@*"/>
                    <xsl:apply-templates/>
                </div>
            </xsl:when>
            <xsl:otherwise>
                <span>
                    <xsl:copy-of select="@*"/>
                    <xsl:apply-templates/>
                </span>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>



</xsl:stylesheet>
