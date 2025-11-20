<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	
	<xsl:param name="hierarchical-level">-1</xsl:param>

	<xsl:output method="xml" omit-xml-declaration="yes" indent="no" encoding="UTF-8"/>

	<xsl:template match="@* | node()">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="h1 | h2 | h3 | h4 | h5 | h6">
		<xsl:variable name="current-header-level">
			<xsl:choose>
				<xsl:when test="local-name() = 'h1'">1</xsl:when>
				<xsl:when test="local-name() = 'h2'">2</xsl:when>
				<xsl:when test="local-name() = 'h3'">3</xsl:when>
				<xsl:when test="local-name() = 'h4'">4</xsl:when>
				<xsl:when test="local-name() = 'h5'">5</xsl:when>
				<xsl:when test="local-name() = 'h6'">6</xsl:when>
				<xsl:otherwise>-1</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>

		<xsl:variable name="current-hierarchical-level">
			<xsl:choose>
				<xsl:when test="$hierarchical-level = -1"><xsl:value-of select="ancestor::*[local-name()='article' or local-name()='div']/@data-hierarchical-level"/></xsl:when>
				<xsl:otherwise><xsl:value-of select="number($hierarchical-level)"/></xsl:otherwise>
			</xsl:choose>
		</xsl:variable>

		<xsl:variable name="new-header-level">
			<xsl:value-of select="number($current-header-level) + number($current-hierarchical-level) - 1"/>
		</xsl:variable>

		<xsl:variable name="new-header-name">
			<xsl:text>h</xsl:text>
			<xsl:choose>
				<xsl:when test="number($new-header-level) > 6">6</xsl:when>
				<xsl:when test="number($new-header-level) = 0">1</xsl:when>
				<xsl:when test="string(number($new-header-level)) = 'NaN'">
					<xsl:value-of select="$current-header-level"/>
				</xsl:when>
				<xsl:otherwise>
					<xsl:value-of select="$new-header-level"/>
				</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>

		<xsl:element name="{$new-header-name}">
			<xsl:copy-of select="@*"/>
			<xsl:attribute name="data-levelcalculated">
				<xsl:value-of select="$new-header-level"/>
			</xsl:attribute>
			<xsl:attribute name="data-leveloriginal">
				<xsl:value-of select="$current-header-level"/>
			</xsl:attribute>
			<xsl:if test="local-name() = 'h1'">
				<xsl:attribute name="data-addsectionnumber">true</xsl:attribute>
			</xsl:if>
			<xsl:apply-templates/>
		</xsl:element>

	</xsl:template>

</xsl:stylesheet>
