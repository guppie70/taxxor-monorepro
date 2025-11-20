<?xml version="1.0"?>
<xsl:stylesheet xmlns="http://www.w3.org/1999/xhtml" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:param name="sectionId"></xsl:param>
	<xsl:param name="output-channel-type"></xsl:param>
	<xsl:param name="renderHtml">yes</xsl:param>

	<xsl:output method="xml" omit-xml-declaration="yes" indent="yes" encoding="UTF-8"/>

	<xsl:template match="@* | * | processing-instruction() | comment()">
        <xsl:copy>
            <xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
        </xsl:copy>
    </xsl:template>
	

    <xsl:template match="div[@class='section'][position()=1]">
    	
<!--    	<xsl:comment>
			sectionId: <xsl:value-of select="$sectionId"/>
			output-channel-type: <xsl:value-of select="$output-channel-type"/>
			renderHtml: <xsl:value-of select="$renderHtml"/>
		</xsl:comment>-->
    	
    	<xsl:choose>
    		<xsl:when test="$output-channel-type = 'web'">
    			<xsl:apply-templates select="node()"/>
    		</xsl:when>
    		<xsl:when test="not($sectionId = 'all')">
    			<xsl:apply-templates select="node()"/>
    		</xsl:when>
    	</xsl:choose>

    </xsl:template>


</xsl:stylesheet>
