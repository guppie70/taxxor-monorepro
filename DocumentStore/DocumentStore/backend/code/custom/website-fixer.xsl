<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    <xsl:output method="xml" indent="yes"/>

    <xsl:template match="@* | * | processing-instruction() | comment()">
        <xsl:copy>
            <xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
        </xsl:copy>
    </xsl:template>
    
    <xsl:template match="article">
        <article>
            <xsl:copy-of select="@*[not(local-name()='data-articletype')]"/>
            <xsl:attribute name="data-articletype">
                <xsl:text>website</xsl:text>
            </xsl:attribute>
            <xsl:apply-templates/>
        </article>
    </xsl:template>

    <xsl:template match="ul/li/a/div">
        <span>
            <xsl:copy-of select="@*"/>
            <xsl:apply-templates/>
        </span>
    </xsl:template>
    
    <xsl:template match="ul/li/a/div[contains(@class, 'c-download__icon')]">
        <span>
            <xsl:copy-of select="@*"/>
            <xsl:comment>.</xsl:comment>
        </span>
    </xsl:template>    
</xsl:stylesheet>
