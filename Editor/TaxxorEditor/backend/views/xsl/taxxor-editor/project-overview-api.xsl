<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:json="http://james.newtonking.com/projects/json" version="1.0">

    <xsl:param name="type">status</xsl:param>

    <xsl:output method="xml" omit-xml-declaration="yes" indent="yes"/>


    <xsl:template match="/">
        <projects>
            <xsl:apply-templates select="/configuration/cms_projects/cms_project[@access='true']"/>
        </projects>
    </xsl:template>
    
    <xsl:template match="cms_project">
        <project id="{@id}" report-type="{@report-type}" status="{versions/version/status/text()}" json:Array="true">
            <xsl:copy-of select="name"/>
            <xsl:copy-of select="reporting_period"/>
            <xsl:copy-of select="versions/version/date_created"/>
            <xsl:copy-of select="versions/version/date_lastmodified"/>
        </project>
        
    </xsl:template>
    
 

</xsl:stylesheet>
