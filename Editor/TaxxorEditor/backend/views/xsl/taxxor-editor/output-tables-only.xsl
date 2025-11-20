<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:param name="output-channel-type">pdf</xsl:param>

	<xsl:output method="xml" omit-xml-declaration="yes" indent="yes" encoding="UTF-8"/>

	<xsl:template match="@* | * | processing-instruction() | comment()">
		<xsl:copy>
			<xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
		</xsl:copy>
	</xsl:template>
	
	<xsl:template match="article[not(div//table)]"/>
		
	

	<xsl:template match="article[div//table]">
		<article>
			<xsl:copy-of select="@*"/>
			<xsl:apply-templates select=".//h1 | .//h2 | .//div[contains(@class, 'table-wrapper') and (table or .//table)]"/>
		</article>
	</xsl:template>
	
	<xsl:template match="table[@data-graph-type]">
		<table>
			<xsl:copy-of select="@*"/>
			<xsl:attribute name="style">
				<xsl:text>display: block!important;</xsl:text>
			</xsl:attribute>
			<xsl:apply-templates/>
		</table>
	</xsl:template>
	
</xsl:stylesheet>
