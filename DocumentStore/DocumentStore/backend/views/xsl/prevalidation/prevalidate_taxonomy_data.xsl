<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:x="http://www.w3.org/2001/XInclude">
	<xsl:param name="currentYear"/>
	<xsl:param name="idContent"/>
	
	<xsl:output encoding="UTF-8" method="xml" indent="yes"/>
	
	<xsl:template match="/">
		<xsl:choose>
			<xsl:when test="$idContent='0'">
				<!-- some dummy data for the cover -->
				<content id="cover" label="Dummy page">
					<concept id="cover" label="Dummy data for cover (to be determined)" abstract="True" period="duration" type="stringItemType"/>
				</content>
			</xsl:when>
			<xsl:otherwise>
				<!-- perform prevalidation on the content of the currently requested section in the xml -->
				<xsl:apply-templates select="taxonomy/linkbase[@type='presentation']/linkrole[@id=$idContent]"/>
			</xsl:otherwise>
		</xsl:choose>	
	</xsl:template>
	
	<!-- copy templates -->
	<xsl:template match="@*|node()">
		<xsl:copy><xsl:apply-templates select="@*|node()"/></xsl:copy>
	</xsl:template>
	
	<xsl:template match="linkrole">
		<content id="{@id}" label="{@label}">
			<xsl:apply-templates select="node()"/>
		</content>
	</xsl:template>
	
	<!-- matches for content that needs to be included from a different url -->
	<xsl:template match="RTFinclude">
		<xsl:variable name="rtf_id" select="@id"/>
		<RTFinclude id="{$rtf_id}"/>
	</xsl:template>


	
</xsl:stylesheet>
