<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	
	<xsl:param name="channel">WEB</xsl:param>

	<xsl:output method="xml" omit-xml-declaration="yes" indent="no" encoding="UTF-8"/>

	<xsl:template match="@* | * | processing-instruction() | comment()">
		<xsl:copy>
			<xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="@*[contains(local-name(), 'data-')]"/>
	
	<!-- 
	<article id="tx1153794-message-from-the-ceo1" 
	data-fact-id="1153794-message-from-the-ceo1" 
	data-guid="1153794-message-from-the-ceo1" 
	data-last-modified="2022-02-11T10:21:25.8071657+00:00" 
	data-description="message-from-the-ceo1" 
	data-articletype="regular" 
	data-hierarchical-level="1" 
	data-ref="1153794-message-from-the-ceo1.xml" 
	data-did="1153794-message-from-the-ceo1" 
	data-tocnumber="1" 
	lang="en">
	-->
	<xsl:template match="@data-articletype" priority="2">
		<xsl:if test="$channel='WEB'">
			<xsl:attribute name="data-articletype">
				<xsl:value-of select="."/>
			</xsl:attribute>
		</xsl:if>
	</xsl:template>
	
	<xsl:template match="@data-variantid" priority="2">
		<xsl:if test="$channel='WEB'">
			<xsl:attribute name="data-variantid">
				<xsl:value-of select="."/>
			</xsl:attribute>
		</xsl:if>
	</xsl:template>
	
	<xsl:template match="@data-hierarchical-level" priority="2">
		<xsl:if test="$channel='WEB'">
			<xsl:attribute name="data-hierarchical-level">
				<xsl:value-of select="."/>
			</xsl:attribute>
		</xsl:if>
	</xsl:template>
	
	<xsl:template match="@data-link-type" priority="2">
		<xsl:if test="$channel='WEB'">
			<xsl:attribute name="data-link-type">
				<xsl:value-of select="."/>
			</xsl:attribute>
		</xsl:if>
	</xsl:template>
	
	<xsl:template match="@data-link-error" priority="2">
		<xsl:if test="$channel='WEB'">
			<xsl:attribute name="data-link-error">
				<xsl:value-of select="."/>
			</xsl:attribute>
		</xsl:if>
	</xsl:template>
	
	<xsl:template match="@data-link-error" priority="2">
		<xsl:if test="$channel='WEB'">
			<xsl:attribute name="data-link-error">
				<xsl:value-of select="."/>
			</xsl:attribute>
		</xsl:if>
	</xsl:template>
	
	<xsl:template match="@data-showheaderdates" priority="2">
		<xsl:if test="$channel='WEB'">
			<xsl:attribute name="data-showheaderdates">
				<xsl:value-of select="."/>
			</xsl:attribute>
		</xsl:if>
	</xsl:template>
	
	<xsl:template match="@data-noteid" priority="2">
		<xsl:if test="$channel='WEB'">
			<xsl:attribute name="data-noteid">
				<xsl:value-of select="."/>
			</xsl:attribute>
		</xsl:if>
	</xsl:template>

	<xsl:template match="@class">
		<xsl:if test="$channel='WEB'">
			<xsl:attribute name="class">
				<xsl:value-of select="."/>
			</xsl:attribute>
		</xsl:if>
	</xsl:template>

</xsl:stylesheet>
