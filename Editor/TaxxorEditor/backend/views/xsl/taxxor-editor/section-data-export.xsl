<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:param name="section-title"/>
	<xsl:param name="projectid"/>
	<xsl:param name="projectname"/>
	<xsl:param name="outputchannel-language"/>
	<xsl:param name="inline-css"/>
	<xsl:param name="data-reference"/>



	<xsl:output method="xml" omit-xml-declaration="yes" indent="no" encoding="UTF-8"/>

	<xsl:template match="@* | * | processing-instruction() | comment()">
		<xsl:copy>
			<xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="/">
		<html>
			<head>
				<meta charset="UTF-8" />
				<title>
					<xsl:value-of select="$section-title"/>
					<xsl:text> - Exported from project </xsl:text>
					<xsl:value-of select="$projectname"/>
				</title>
				<xsl:if test="string-length(normalize-space($inline-css)) > 0">
					<style>
						<xsl:value-of select="$inline-css"/>
					</style>
				</xsl:if>
			</head>
			<body data-projectid="{$projectid}" data-sourcelang="{$outputchannel-language}" data-ref="{$data-reference}">
				<xsl:apply-templates/>
			</body>
		</html>

	</xsl:template>


</xsl:stylesheet>
