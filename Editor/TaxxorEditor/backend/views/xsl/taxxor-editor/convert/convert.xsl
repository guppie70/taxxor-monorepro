<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:param name="nodeName">Bla</xsl:param>

	<xsl:variable name="smallcase" select="'abcdefghijklmnopqrstuvwxyz'"/>
	<xsl:variable name="uppercase" select="'ABCDEFGHIJKLMNOPQRSTUVWXYZ'"/>

	<xsl:output method="xml" omit-xml-declaration="yes" indent="no" encoding="UTF-8"/>

	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="system">
		<system>
			<xsl:apply-templates select="*[not(local-name(.)='web_page')]"/>
		</system>

	</xsl:template>

	<xsl:template match="text">
		<text>
			<xsl:attribute name="pathWeb">
				<xsl:call-template name="render-url"/>
			</xsl:attribute>
			<xsl:apply-templates select="@*[not(local-name(.)='pathWeb')]"/>
			<xsl:apply-templates select="node()"/>
		</text>

	</xsl:template>

	<xsl:template match="processing-instruction()"/>

	<xsl:template name="render-url">
		<xsl:text>/</xsl:text>
		<xsl:value-of select="translate($nodeName, $uppercase , $smallcase)"/>
		<xsl:text>.html</xsl:text>
	</xsl:template>

</xsl:stylesheet>
