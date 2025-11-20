<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">

    <xsl:param name="lang">en</xsl:param>

    <xsl:output method="xml" encoding="UTF-8" indent="yes" omit-xml-declaration="yes"/>

    <xsl:template match="@* | * | processing-instruction() | comment()" mode="identity-copy">
        <xsl:copy>
            <xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()" mode="identity-copy"/>
        </xsl:copy>
    </xsl:template>

    <xsl:template match="/">
        <xsl:choose>
            <xsl:when test="$lang = 'all'">
                <xsl:apply-templates select="data/content"/>
            </xsl:when>
            <xsl:otherwise>
                <xsl:apply-templates select="data/content[@lang = $lang]"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <xsl:template match="content">
        <xsl:apply-templates mode="identity-copy"/>
    </xsl:template>
    
    <!-- Stuff that we want to change goes below -->
    <!-- <xsl:template match="section[not(div/@id='header-logo')][not(@class='two-columns__left')]" mode="identity-copy"> -->
    <!-- <xsl:template match="section[not(div/@id='header-logo')]" mode="identity-copy">
        <section>
            <xsl:copy-of select="@*"/>
            <xsl:attribute name="data-editable">true</xsl:attribute>
            <xsl:apply-templates mode="identity-copy"/>
        </section>
    </xsl:template>

    <xsl:template match="section[div/@id='header-logo']//*[a/@class]" mode="identity-copy">
        <xsl:element name="{local-name()}">
            <xsl:copy-of select="@*"/>
            <xsl:attribute name="data-editable">true</xsl:attribute>
            <xsl:attribute name="data-fixture">true</xsl:attribute>
            
            <xsl:apply-templates mode="identity-copy"/>
        </xsl:element>
    </xsl:template>
    <xsl:template match="table//div" mode="identity-copy">
        <xsl:element name="{local-name()}">
            <xsl:copy-of select="@*"/>

            <xsl:attribute name="data-editable">true</xsl:attribute>
            
            <xsl:apply-templates mode="identity-copy"/>
        </xsl:element>
    </xsl:template> -->

</xsl:stylesheet>
