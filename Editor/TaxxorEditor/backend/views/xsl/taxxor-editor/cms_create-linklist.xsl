<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:param name="pageid">none</xsl:param>
	<xsl:param name="parentpageid">none</xsl:param>
	<xsl:output method="xml" omit-xml-declaration="yes"/>

	<xsl:template match="/">

		<div class="cms_link-list">
			<ul>

				<xsl:choose>
					<xsl:when test="$pageid='none' and $parentpageid='none'">
						<xsl:apply-templates select="/items/structured/item"/>
					</xsl:when>
					<xsl:when test="$parentpageid='none'">
						<xsl:apply-templates select="/items/structured//item[@id=$pageid]"/>
					</xsl:when>
					<xsl:otherwise>
						<xsl:apply-templates select="/items/structured//item[@id=$parentpageid]/sub_items/item"/>
					</xsl:otherwise>
				</xsl:choose>
			</ul>

		</div>

	</xsl:template>

	<xsl:template match="item">

		<li>
			<div class="cms_link-wrapper">
				<xsl:choose>
					<xsl:when test="web_page/path=''">
						<xsl:value-of select="web_page/linkname"/>
					</xsl:when>
					<xsl:otherwise>
						<a class="cms_link">
							<xsl:attribute name="href">
								<xsl:value-of select="web_page/path"/>
							</xsl:attribute>
							<xsl:value-of select="web_page/linkname"/>
						</a>
					</xsl:otherwise>
				</xsl:choose>
			</div>

			<xsl:if test="sub_items/item">
				<xsl:apply-templates select="sub_items"/>
			</xsl:if>
		</li>

	</xsl:template>

	<xsl:template match="sub_items">
		<ul>
			<xsl:apply-templates select="item"/>
		</ul>
	</xsl:template>

</xsl:stylesheet>
