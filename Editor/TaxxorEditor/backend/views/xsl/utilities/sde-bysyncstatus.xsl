<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    <xsl:param name="status">invalid</xsl:param>
    
    <xsl:output method="xml" indent="yes"/>
    
    
    <xsl:template match="/">
        <structured-data-elements synctatusfragment="{$status}">
            <xsl:apply-templates select="//*[@data-fact-id and not(local-name()='article') and contains(@data-syncstatus, $status)]"></xsl:apply-templates>
        </structured-data-elements>
    </xsl:template>
    
    <xsl:template match="*">
        <element factid="{@data-fact-id}">
            <xsl:attribute name="data-ref">
                <xsl:value-of select="ancestor::article/@data-ref"/>
            </xsl:attribute>
            <xsl:value-of select="text()"/>
        </element>
    </xsl:template>
    
    
</xsl:stylesheet>
