<?xml version='1.0'?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	
	<xsl:output method="html" omit-xml-declaration="yes"/>
	
	
	<xsl:template match="/">
		<xsl:text>var apiRoutes={</xsl:text>
		<xsl:for-each select="//item[@id='apiroot']//item[not(web_page/path = '#')]">
			<xsl:text>'</xsl:text>
			<xsl:value-of select="@id"/>
			<xsl:text>': '</xsl:text>
			<xsl:value-of select="web_page/path"/>
			<xsl:text>'</xsl:text>
			<xsl:if test="not(position()=last())">
				<xsl:text>, </xsl:text>
			</xsl:if>
		</xsl:for-each>		
		<xsl:text>};</xsl:text>
		
<xsl:text>

</xsl:text>

	</xsl:template>
	
	
</xsl:stylesheet>
