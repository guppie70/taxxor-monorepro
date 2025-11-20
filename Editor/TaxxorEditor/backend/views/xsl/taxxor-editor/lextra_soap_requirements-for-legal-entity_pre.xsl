<?xml version='1.0'?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<xsl:param name="guidLegalEntity"/>
	<xsl:output method="xml" omit-xml-declaration="yes" encoding="UTF-8" indent="yes"/>

	<xsl:template match="node()|text()|@*">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="Body">
		<xsl:apply-templates select="GetRequirementsForLegalEntity2Response"/>
	</xsl:template>

	<xsl:template match="GetRequirementsForLegalEntity2Response">
		<reporting_requirements guidLegalEntity="{$guidLegalEntity}">
			<xsl:text>
				
			</xsl:text>
			<xsl:apply-templates select="GetRequirementsForLegalEntity2Result/requirement"/>
		</reporting_requirements>
	</xsl:template>

	<xsl:template match="requirement">
		<reporting_requirement>
			<xsl:attribute name="guidLegalEntityRequirement">
				<xsl:value-of select="guid"/>
			</xsl:attribute>
			<xsl:attribute name="modified">
				<xsl:value-of select="modified"/>
			</xsl:attribute>

			<name>
				<xsl:value-of select="name"/>
			</name>
			<requirement>
				<xsl:apply-templates/>
			</requirement>


		</reporting_requirement>
	</xsl:template>

	<xsl:template match="requirementSchedule/*">
		<xsl:variable name="node_name" select="local-name(.)"/>
		<xsl:variable name="node_text" select="./text()"/>
		<xsl:element name="{$node_name}">
			
			<xsl:if test="contains($node_name, 'Date')">
				<xsl:attribute name="isoDate">
					<xsl:value-of select="substring-before($node_text, 'T')"/>
				</xsl:attribute>
				<xsl:call-template name="parse_iso_date">
					<xsl:with-param name="date_part" select="substring-before($node_text, 'T')"/>
				</xsl:call-template>
			</xsl:if>

			<xsl:value-of select="./text()"/>
		</xsl:element>

	</xsl:template>

	<xsl:template match="requirement/guid"/>
	<xsl:template match="requirement/modified"/>
	<xsl:template match="requirement/name"/>

	<!-- parses an ISO date into seperate attributes for year, month, day -->
	<xsl:template name="parse_iso_date">
		<xsl:param name="date_part"/>
		<xsl:param name="counter">0</xsl:param>

		<xsl:variable name="attribute_name">
			<xsl:choose>
				<xsl:when test="number($counter)=0">year</xsl:when>
				<xsl:when test="number($counter)=1">month</xsl:when>
				<xsl:when test="number($counter)=2">day</xsl:when>
			</xsl:choose>
		</xsl:variable>

		<xsl:attribute name="{$attribute_name}">
			<xsl:choose>
				<xsl:when test="contains($date_part,'-')">
					<xsl:value-of select="substring-before($date_part, '-')"/>
				</xsl:when>
				<xsl:otherwise>
					<xsl:value-of select="$date_part"/>
				</xsl:otherwise>
			</xsl:choose>
		</xsl:attribute>

		<xsl:if test="number($counter) &lt; 2">
			<xsl:call-template name="parse_iso_date">
				<xsl:with-param name="counter" select="number($counter) + 1"/>
				<xsl:with-param name="date_part" select="substring-after($date_part, '-')"/>
			</xsl:call-template>
		</xsl:if>

	</xsl:template>

</xsl:stylesheet>
