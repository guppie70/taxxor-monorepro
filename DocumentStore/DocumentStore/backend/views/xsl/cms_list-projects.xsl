<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">

    <xsl:output method="xml" omit-xml-declaration="no" indent="yes" encoding="UTF-8"/>

    <xsl:template match="@*|node()" mode="identity-copy">
        <xsl:copy>
            <xsl:apply-templates select="@*|node()" mode="identity-copy"/>
        </xsl:copy>
    </xsl:template>
    
    <xsl:template match="/">
        <projects>
            <xsl:apply-templates select="/configuration/cms_projects/cms_project"/>
        </projects>
        
    </xsl:template>

    <xsl:template match="cms_project">
        <project>
            <xsl:copy-of select="@*"/>
            <xsl:apply-templates mode="identity-copy"/>
        </project>
    </xsl:template>
    

</xsl:stylesheet>
