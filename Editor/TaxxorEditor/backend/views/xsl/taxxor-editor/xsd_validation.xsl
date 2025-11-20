<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:param name="lang">en</xsl:param>

	<xsl:output method="xml" omit-xml-declaration="yes" indent="no" encoding="UTF-8" cdata-section-elements="stylefoo"/>

	<xsl:template match="@* | * | processing-instruction() | comment()">
		<xsl:copy>
			<xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="html">
		<html xml:lang="{$lang}">
			<xsl:apply-templates/>
		</html>
	</xsl:template>


	<xsl:template match="article">
		<div data-originalnodename="{local-name()}" class="article">
			<xsl:copy-of select="@*[not(contains(local-name(), 'tangelo') or local-name() = 'modified-by' or local-name() = 'lang')]"/>
			<xsl:apply-templates/>
		</div>
	</xsl:template>

	
	<xsl:template match="section">
		<div data-originalnodename="{local-name()}" class="section">
			<xsl:copy-of select="@*"/>
			<xsl:apply-templates/>
		</div>
	</xsl:template>
	
	<!-- /data/content[1]/article[1]/div[1]/section[1]/div[1]/div[2]/div[1]/svg[1]/@class 
	/data/content[1]/article[1]/div[1]/section[1]/div[1]/div[2]/div[1]/*[namespace-uri()='http://www.w3.org/2000/svg' and local-name()='svg'][1]/*[namespace-uri()='http://www.w3.org/2000/svg' and local-name()='desc'][1]
	-->
	<!--
	<xsl:template match="svg"/>
	<xsl:template match="*[namespace-uri()='http://www.w3.org/2000/svg' and local-name()='svg']"/>
	-->
	
	<xsl:template match="*[@data-footnoteid and @id]">
		<xsl:variable name="element-name" select="local-name()"/>
		<xsl:element name="{$element-name}">
			<xsl:copy-of select="@*[not(local-name() = 'id')]"/>
			<xsl:apply-templates/>
		</xsl:element>
	</xsl:template>
	
	<xsl:template match="@_msttexthash"/>
	<xsl:template match="@_msthash"/>
	<xsl:template match="@_mstmutation"/>
	<xsl:template match="@_msthidden"/>
</xsl:stylesheet>
