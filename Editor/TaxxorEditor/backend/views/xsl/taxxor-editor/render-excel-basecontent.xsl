<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:msxsl="urn:schemas-microsoft-com:xslt" version="1.0">
    <xsl:param name="mode">load</xsl:param>
    <xsl:param name="project-id">q218</xsl:param>
    <xsl:param name="taxxorClientId">default</xsl:param>

    <xsl:output method="xml" omit-xml-declaration="yes" indent="yes"/>
   

    <xsl:template match="@* | * | processing-instruction()" mode="identitycopy">
        <xsl:copy>
            <xsl:apply-templates select="* | @* | text() | processing-instruction()" mode="identitycopy"/>
        </xsl:copy>
    </xsl:template>
    
    <xsl:template match="/">
        
        <xsl:choose>
            <xsl:when test="$taxxorClientId='philips'"></xsl:when>
            <xsl:otherwise></xsl:otherwise>
        </xsl:choose>
        
        <xsl:variable name="sectortitle">
            <xsl:choose>
                <xsl:when test="$taxxorClientId='philips'">
                    <xsl:value-of select="/div/div[@class='tablegraph-header-wrapper']/p[@class='sectortitle']"/>
                </xsl:when>
                <xsl:otherwise></xsl:otherwise>
            </xsl:choose> 
        </xsl:variable>
        <xsl:variable name="maintitle">
            <xsl:choose>
                <xsl:when test="$taxxorClientId='philips'">
                    <xsl:value-of select="/div/div[@class='tablegraph-header-wrapper']/div/p[contains(@class, 'maintitle')]"/>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:value-of select="/div/div[@class='tablegraph-header-wrapper']//div[@class='table-title']"/>
                </xsl:otherwise>
            </xsl:choose>
        </xsl:variable>
        <xsl:variable name="unittititle">
            <xsl:choose>
                <xsl:when test="$taxxorClientId='philips'">
                    <xsl:value-of select="/div/div[@class='tablegraph-header-wrapper']/div/p[contains(@class, 'unittitle')]"/>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:value-of select="/div/div[@class='tablegraph-header-wrapper']//div[@class='table-scale']"/>
                </xsl:otherwise>
            </xsl:choose>
        </xsl:variable>
        <xsl:variable name="datatitle">
            <xsl:choose>
                <xsl:when test="$taxxorClientId='philips'">
                    <xsl:value-of select="/div/div[@class='tablegraph-header-wrapper']/p[@class='datatitle']"/>
                </xsl:when>
                <xsl:otherwise></xsl:otherwise>
            </xsl:choose>
        </xsl:variable>
        
        <xsl:apply-templates select="/div/table">
            <xsl:with-param name="sectortitle" select="$sectortitle"/>
            <xsl:with-param name="maintitle" select="$maintitle"/>
            <xsl:with-param name="unittititle" select="$unittititle"/>
            <xsl:with-param name="datatitle" select="$datatitle"/>      
        </xsl:apply-templates>
    </xsl:template>
    
    
    
    <xsl:template match="table">
        <xsl:param name="sectortitle"/>
        <xsl:param name="maintitle"/>
        <xsl:param name="unittititle"/>
        <xsl:param name="datatitle"/>
        <table>
            <xsl:apply-templates select="thead">
                <xsl:with-param name="sectortitle" select="$sectortitle"/>
                <xsl:with-param name="maintitle" select="$maintitle"/>
                <xsl:with-param name="unittititle" select="$unittititle"/>
                <xsl:with-param name="datatitle" select="$datatitle"/>      
            </xsl:apply-templates>
            <xsl:apply-templates select="tbody" mode="identitycopy"/>
        </table>
        
    </xsl:template>
    
    <xsl:template match="thead">
        <xsl:param name="sectortitle"/>
        <xsl:param name="maintitle"/>
        <xsl:param name="unittititle"/>
        <xsl:param name="datatitle"/>
        <thead>
            <xsl:variable name="col-spans" select="sum(tr[1]/th[@colspan]/@colspan)"/>
            <xsl:variable name="no-colspan-cells" select="count(tr[1]/th[not(@colspan)])"/>
            
            <xsl:variable name="nr-of-cells">
                <xsl:value-of select="$col-spans + $no-colspan-cells"/>
            </xsl:variable>
            
            <xsl:call-template name="render-header-row">
                <xsl:with-param name="text" select="$sectortitle"/>
                <xsl:with-param name="colspan" select="$nr-of-cells"/>
                <xsl:with-param name="type">sectortitle</xsl:with-param>
            </xsl:call-template>
            <xsl:call-template name="render-header-row">
                <xsl:with-param name="text" select="$maintitle"/>
                <xsl:with-param name="colspan" select="$nr-of-cells"/>
                <xsl:with-param name="type">maintitle</xsl:with-param>
            </xsl:call-template>
            <xsl:call-template name="render-header-row">
                <xsl:with-param name="text" select="$unittititle"/>
                <xsl:with-param name="colspan" select="$nr-of-cells"/>
                <xsl:with-param name="type">unittititle</xsl:with-param>
            </xsl:call-template>
            <xsl:call-template name="render-header-row">
                <xsl:with-param name="text" select="$datatitle"/>
                <xsl:with-param name="colspan" select="$nr-of-cells"/>
                <xsl:with-param name="type">datatitle</xsl:with-param>
            </xsl:call-template>            
            
            
            <xsl:apply-templates mode="identitycopy"/>
        </thead>
        
        
    </xsl:template>
    
    <xsl:template match="tr[@class]" mode="identitycopy">
        <tr>
<!--            <xsl:call-template name="element-is-hidden">
                <xsl:with-param name="class" select="@class"/> 
            </xsl:call-template>
            -->
            <xsl:copy-of select="@*[not(local-name()='class')]"/>
            <xsl:apply-templates mode="identitycopy"/>
        </tr>
    </xsl:template>
    
    <xsl:template match="th" mode="identitycopy">
        <xsl:variable name="column-number">
            <xsl:choose>
                <xsl:when test="count(parent::tr/th[@colspan]) > 0">
                    <xsl:call-template name="get-cell-column-nr">
                        <xsl:with-param name="cell" select="."/>
                    </xsl:call-template>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:text>na</xsl:text>
                </xsl:otherwise>
            </xsl:choose>
        </xsl:variable>
        
        <th data-emptyvalue="">
            <xsl:if test="not($column-number = 'na')">
                <xsl:attribute name="data-colnumber">
                    <xsl:value-of select="$column-number"/>
                </xsl:attribute>
            </xsl:if>
            
            <xsl:call-template name="element-is-hidden">
                <xsl:with-param name="class" select="concat(@class, ' ', ../@class)"/> 
            </xsl:call-template>
            
            <xsl:copy-of select="@*[not(local-name()='class')]"/>
            
            <xsl:apply-templates mode="identitycopy"/>
            <xsl:text> </xsl:text>
        </th>
    </xsl:template>
    
    <xsl:template match="td" mode="identitycopy">
        <xsl:variable name="text-align">
            <xsl:choose>
                <xsl:when test="count(preceding-sibling::*[self::td or self::th]) = 0">left</xsl:when>
                <xsl:otherwise>right</xsl:otherwise>
            </xsl:choose>
        </xsl:variable>
        
        <xsl:variable name="column-number">
            <xsl:choose>
                <xsl:when test="count(parent::tr/td[@colspan]) > 0">
                    <xsl:call-template name="get-cell-column-nr">
                        <xsl:with-param name="cell" select="."/>
                    </xsl:call-template>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:text>na</xsl:text>
                </xsl:otherwise>
            </xsl:choose>
        </xsl:variable>
        
        <td align="{$text-align}" data-emptyvalue="">
            <xsl:if test="not($column-number = 'na')">
                <xsl:attribute name="data-colnumber">
                    <xsl:value-of select="$column-number"/>
                </xsl:attribute>
            </xsl:if>
            
            <xsl:call-template name="element-is-hidden">
                <xsl:with-param name="class" select="concat(@class, ' ', ../@class)"/> 
            </xsl:call-template>
            
            <xsl:copy-of select="@*[not(local-name()='class' or local-name()='data-cellidentifier')]"/>
            
            <xsl:apply-templates mode="identitycopy"/>
        </td>
    </xsl:template>
    
    <xsl:template match="p" mode="identitycopy">
        <xsl:apply-templates mode="identitycopy"/>
    </xsl:template>
    
    <xsl:template match="sup[contains(@class, 'fn')]" mode="identitycopy"/>
    
    <xsl:template match="span | strong | i | b | em" mode="identitycopy">
        <xsl:apply-templates mode="identitycopy"/>
    </xsl:template>
    
    <xsl:template name="render-header-row">
        <xsl:param name="colspan"/>
        <xsl:param name="text"/>
        <xsl:param name="type"/>
        <xsl:if test="string-length(normalize-space($text)) > 0">
            <tr>
                <th hide="false" data-headertype="{$type}" data-colnumber="1">
                    <xsl:if test="number($colspan) > 1">
                        <xsl:attribute name="colspan">
                            <xsl:value-of select="$colspan"/>
                        </xsl:attribute>
                    </xsl:if>
                    <xsl:choose>
                        <xsl:when test="$taxxorClientId='philips'">
                            <xsl:apply-templates select="msxsl:node-set($text)" mode="identitycopy"/>
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:value-of select="$text"/>
                        </xsl:otherwise>
                    </xsl:choose>
                    <xsl:text> </xsl:text>
                </th>
            </tr>
        </xsl:if>
    </xsl:template>
    
    <xsl:template name="get-cell-column-nr">
        <xsl:param name="cell"/>
        <!-- get position of cell, by also accounting for colspans -->
        <xsl:variable name="col-spans" select="sum($cell/preceding-sibling::*[(self::td or self::th) and @colspan]/@colspan)"/>
        <xsl:variable name="no-colspan-cells" select="count($cell/preceding-sibling::*[(self::td or self::th) and not(@colspan)]) + 1"/>
        <xsl:value-of select="$col-spans + $no-colspan-cells"/>
    </xsl:template>
    
    <xsl:template name="element-is-hidden">
        <xsl:param name="class"/>
        <xsl:choose>
            <xsl:when test="contains($class, 'hide')">
                <xsl:attribute name="hide">true</xsl:attribute>
            </xsl:when>
            <xsl:otherwise>
                <xsl:attribute name="hide">false</xsl:attribute>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>
    
</xsl:stylesheet>
