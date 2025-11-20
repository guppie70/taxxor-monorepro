<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">

	<xsl:output method="xml" omit-xml-declaration="yes" indent="no" encoding="UTF-8"/>

	<xsl:template match="@* | * | processing-instruction() | comment()">
		<xsl:copy>
			<xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
		</xsl:copy>
	</xsl:template>
	
	<!-- change the meta tag in the head -->
	<xsl:template match="head">
		<head>
			<meta charset="UTF-8"/>
			<xsl:apply-templates select="*[not(local-name()='meta')]"/>
			<xsl:apply-templates select="comment()"/>
		</head>
	</xsl:template>


	<!-- strip table head and table body -->
	<xsl:template match="thead | tbody">
		<xsl:apply-templates select="node()"/>
	</xsl:template>

	<xsl:template match="tr[th]">
		<tr>
			<xsl:copy-of select="@*[not(local-name() = 'class')]"/>
			<xsl:attribute name="class">
				<xsl:choose>
					<xsl:when test="@class">
						<xsl:value-of select="concat(@class, ' rowheader')"/>
					</xsl:when>
					<xsl:otherwise>rowheader</xsl:otherwise>
				</xsl:choose>
			</xsl:attribute>
			<xsl:apply-templates/>
		</tr>
	</xsl:template>

	<xsl:template match="th">
		<td>
			<xsl:copy-of select="@*"/>
			<xsl:apply-templates/>
		</td>
	</xsl:template>
	
	<xsl:template match="span">
		<small>
			<xsl:copy-of select="@*[local-name() = 'class' or local-name() = 'id' or local-name() = 'style']"/>
			<xsl:apply-templates/>
		</small>
	</xsl:template>
	
	
	<xsl:template match="content">
		<xsl:apply-templates/>
	</xsl:template>



</xsl:stylesheet>
