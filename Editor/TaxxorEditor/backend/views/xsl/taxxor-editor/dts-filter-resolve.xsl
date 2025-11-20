<?xml version='1.0'?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">


	<xsl:output method="xml" omit-xml-declaration="yes" indent="yes"/>
	<xsl:strip-space elements="*"/>

	<xsl:template match="/">
		<taxonomy view="default">
			<xsl:apply-templates select="/dtsInfo/dtsPresentation/role"/>
		</taxonomy>
	</xsl:template>

	<xsl:template match="role">
		<role>
			<label>
				<xsl:value-of select="@label"/>
			</label>
			<roletype>
				<xsl:value-of select="@roletype"/>
			</roletype>

			<xsl:apply-templates select="item"/>

		</role>
	</xsl:template>

	<xsl:template match="item">
		<xsl:variable name="item-ref" select="@ref"/>
		<xsl:variable name="node-element" select="/dtsInfo/dtsElements/dtsElement[@index = $item-ref]"/>

		<xsl:choose>
			<xsl:when test="count($node-element) = 0">
				<error>
					<xsl:text>No element found for reference: </xsl:text>
					<xsl:value-of select="$item-ref"/>
				</error>
			</xsl:when>
			<xsl:otherwise>
				<element>
					<xsl:for-each select="$node-element/@*">
						<xsl:element name="{local-name(.)}">
							<xsl:value-of select="."/>
						</xsl:element>
					</xsl:for-each>
					<label>
						<xsl:value-of select="$node-element/text()"/>
					</label>
					<xsl:apply-templates select="item"/>
				</element>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>


</xsl:stylesheet>
