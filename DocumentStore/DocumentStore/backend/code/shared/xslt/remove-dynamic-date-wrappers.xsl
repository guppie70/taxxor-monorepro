<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	
	<xsl:param name="output-channel-language"/>
	
	<xsl:output method="xml" omit-xml-declaration="yes" indent="no" encoding="UTF-8"/>
	
	<xsl:template match="@* | * | processing-instruction() | comment()">
		<xsl:copy>
			<xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
		</xsl:copy>
	</xsl:template>
	
	<xsl:template match="*[span/@class='txdynamicdate' and span/@data-dateflexible='false']">
		<xsl:element name="{local-name()}">
			<xsl:copy-of select="@*"/>
			<!-- Store the setting on the parent node so that we can re-apply it in case we need to render a new dynamic date wrapper again -->
			<xsl:attribute name="data-dateflexiblestored">false</xsl:attribute>
			<xsl:apply-templates/>
		</xsl:element>
	</xsl:template>
	
	<xsl:template match="span[@class='txdynamicdate']">
		<xsl:choose>
			<xsl:when test="not(ancestor::table)">
				<span>
					<xsl:copy-of select="@*" />
					<xsl:apply-templates/>
				</span>
			</xsl:when>
			<xsl:when test="$output-channel-language = 'all' or ancestor::content[@lang=$output-channel-language]">
				<xsl:apply-templates/>
			</xsl:when>
			<xsl:otherwise>
				<span>
					<xsl:copy-of select="@*" />
					<xsl:apply-templates/>
				</span>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>
	
</xsl:stylesheet>
