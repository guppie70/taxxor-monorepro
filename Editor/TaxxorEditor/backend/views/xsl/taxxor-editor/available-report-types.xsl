<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    
    <xsl:param name="filter-editor-id"></xsl:param>
    
    <xsl:output method="xml" omit-xml-declaration="yes" indent="yes"/>


    <xsl:template match="/">
        <reports>
            <xsl:choose>
                <xsl:when test="string-length(normalize-space($filter-editor-id)) = 0">
                    <xsl:apply-templates select="/configuration/report_types/report_type"/>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:apply-templates select="/configuration/report_types/report_type[contains(@editorId, $filter-editor-id)]"/>
                </xsl:otherwise>
            </xsl:choose>
            
        </reports> 
    </xsl:template>
    
    <xsl:template match="report_type">
        <report>
            <id><xsl:value-of select="@id"/></id>
            <editorid><xsl:value-of select="@editorId"/></editorid>
            <name><xsl:value-of select="name"/></name>
            <entrypoints>
                <xsl:apply-templates select="entry_points/uri"/>
            </entrypoints>
        </report>
    </xsl:template>
    
    <xsl:template match="uri">
        <uri>
            <xsl:value-of select="."/>
        </uri>
    </xsl:template>
    
</xsl:stylesheet>
