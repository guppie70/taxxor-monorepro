<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:param name="output-channel-type">pdf</xsl:param>

	<xsl:output method="xml" omit-xml-declaration="yes" indent="no" encoding="UTF-8"/>

	<xsl:template match="@* | * | processing-instruction() | comment()">
		<xsl:copy>
			<xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
		</xsl:copy>
	</xsl:template>
	
	<xsl:template match="this | data | maturing">
		<xsl:apply-templates/>
	</xsl:template>
	
	<xsl:template match="ul[not(li)] | ol[not(li)]">
	</xsl:template>
	
	<!-- Remove <thead> elements if they do not contain any <tr> elements -->
	<xsl:template match="thead[not(tr)]" />
	
	<!-- Remove <tbody> elements if they do not contain any <tr> elements -->
	<xsl:template match="tbody[not(tr)]" />
	
	<!-- Remove <tr> elements if they do not contain any <td> or <th> elements -->
	<xsl:template match="tr[not(td) and not(th)]" />
	
	<!-- Ensure that an alt attribute is always present for an image -->
	<xsl:template match="img[not(@alt)]">
		<img alt="Image">
			<xsl:copy-of select="@*"/>
			<xsl:apply-templates/>
		</img>
	</xsl:template>

</xsl:stylesheet>
