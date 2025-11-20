<?xml version='1.0'?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">


	<xsl:output method="xml" indent="yes" encoding="UTF-8" omit-xml-declaration="yes"/>

	<!-- Idenitity copy stylesheet -->
	<xsl:template match="@* | node()">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()"/>
		</xsl:copy>
	</xsl:template>


	<xsl:template match="/">
		<overview>
			<outputchannels>
				<xsl:apply-templates select="hierarchies/output_channel"/>
			</outputchannels>
			<orphaned>
				<xsl:apply-templates select="hierarchies/item_overview/*"/>
			</orphaned>
			<datareferences>
				<xsl:apply-templates select="hierarchies/datareferences/*"/>
			</datareferences>
		</overview>

	</xsl:template>


	<xsl:template match="item">
		<item>
			<xsl:copy-of select="@*[not(
				local-name() = 'data-articleid' or 
				local-name() = 'data-articletitle' or 
				local-name() = 'level' or 
				local-name() = 'data-tocstart' or 
				local-name() = 'data-tocstart' or 
				local-name() = 'data-tocend' or 
				local-name() = 'data-tocstyle' or 
				local-name() = 'data-tocnumber')]"/>
			<xsl:apply-templates/>
		</item>
	</xsl:template>

	<xsl:template match="web_page">
		<name>
			<xsl:copy-of select="linkname/node()"/>
		</name>
	</xsl:template>
	
	<xsl:template match="h1">
		<name>
			<xsl:apply-templates/>
		</name>
	</xsl:template>
	
	<xsl:template match="article">
		<article>
			<xsl:copy-of select="@id"/>
			<xsl:apply-templates/>
		</article>
	</xsl:template>


</xsl:stylesheet>
