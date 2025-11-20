<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

	<xsl:param name="projectid"/>

	<xsl:output encoding="UTF-8" indent="yes" method="xml" omit-xml-declaration="yes"/>

	<xsl:template match="/">
		<xsl:for-each select="/configuration/cms_projects/cms_project[@id = $projectid]/reporting_requirements/reporting_requirement">
			<xsl:if test="not(@ref-taxonomy) or not(preceding-sibling::reporting_requirement[@ref-taxonomy = current()/@ref-taxonomy])">
				<xsl:apply-templates select="."/>
			</xsl:if>
		</xsl:for-each>
	</xsl:template>


	<xsl:template match="reporting_requirement">
		<xsl:variable name="scheme">
			<xsl:choose>
				<xsl:when test="@ref-taxonomy"><xsl:value-of select="@ref-taxonomy"/></xsl:when>
				<xsl:otherwise><xsl:value-of select="@ref-mappingservice"/></xsl:otherwise>
			</xsl:choose>			
		</xsl:variable>

        <xsl:variable name="name">
			<xsl:choose>
				<xsl:when test="@ref-taxonomy"><xsl:value-of select="@ref-taxonomy"/></xsl:when>
				<xsl:otherwise><xsl:value-of select="name"/></xsl:otherwise>
			</xsl:choose>			
		</xsl:variable>
		
		<div class="checkbox reportingrequirement scheme-{$scheme} outputchannelvariant-{@ref-outputchannelvariant} format-{@format}">
			<label>
				<input type="checkbox" data-mappingservicescheme="{$scheme}"/>
				<xsl:text> </xsl:text>
				<xsl:value-of select="$name"/>
			</label>
		</div>
	</xsl:template>

</xsl:stylesheet>
