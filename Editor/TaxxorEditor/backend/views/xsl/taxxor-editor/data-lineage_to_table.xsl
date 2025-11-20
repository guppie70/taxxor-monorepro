<?xml version="1.0"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:json="http://james.newtonking.com/projects/json">
	<xsl:param name="worksheetname">report</xsl:param>

	<xsl:output method="xml" omit-xml-declaration="yes" indent="yes"/>


	<xsl:template match="/">
		<table data-worksheetname="{$worksheetname}">
			<thead>
				<tr json:Array="true">
					<th>Section</th>
					<th>Identifier</th>
					<th>Period start</th>
					<th>Period end</th>
					<th>Data type</th>
					<th>Source</th>
					<th>Hierarchy</th>
					<th>Item</th>
					<th>Additional context</th>
					<th>Value</th>
					<th>Displayed value</th>
					<th>Reported XBRL value</th>
					<th>Taxonomy</th>
					<th>XBRL concept</th>
					<th>Dimensions/members</th>
					<th>Error</th>
				</tr>
			</thead>
			<tbody>
				<xsl:apply-templates select="/datalineage/article"/>
			</tbody>
		</table>
	</xsl:template>

	<xsl:template match="article">
		<xsl:variable name="normalized-article-title" select="@title"/>
		<xsl:apply-templates select="item">
			<xsl:with-param name="normalized-article-title" select="$normalized-article-title"/>
		</xsl:apply-templates>
	</xsl:template>

	<xsl:template match="item">
		<xsl:param name="normalized-article-title"/>

		<tr json:Array="true">
			<td data-align="left" data-emptyvalue="">
				<xsl:value-of select="$normalized-article-title"/>
			</td>
			<td data-align="left" data-emptyvalue="">
				<xsl:value-of select="@id"/>
			</td>
			<td data-align="left" data-emptyvalue="">
				<xsl:value-of select="@periodStart"/>
			</td>
			<td data-align="left" data-emptyvalue="">
				<xsl:value-of select="@periodEnd"/>
			</td>
			<td data-align="left" data-emptyvalue="">
				<xsl:value-of select="@dataType"/>
			</td>
			<td data-align="left" data-emptyvalue="">
				<xsl:value-of select="source/@dataSet"/>
			</td>
			<td data-align="left" data-emptyvalue="">
				<xsl:value-of select="source/@hierarchy"/>
			</td>
			<td data-align="left" data-emptyvalue="">
				<xsl:for-each select="source/member">
					<xsl:value-of select="."/>
					<xsl:if test="not(position() = last())">
						<xsl:text>,</xsl:text>
					</xsl:if>
				</xsl:for-each>
			</td>
			<td data-align="left" data-emptyvalue="">
				<xsl:for-each select="source/dimension">
					<xsl:value-of select="@dimension"/>
					<xsl:text>=</xsl:text>
					<xsl:for-each select="member">
						<xsl:value-of select="."/>
						<xsl:if test="not(position() = last())">
							<xsl:text>;</xsl:text>
						</xsl:if>
					</xsl:for-each>
					<xsl:if test="not(position() = last())">
						<xsl:text>,</xsl:text>
					</xsl:if>
				</xsl:for-each>
			</td>
			<td data-emptyvalue="">
				<xsl:value-of select="sourceValue"/>
			</td>
			<td data-emptyvalue="">
				<xsl:value-of select="reportValue"/>
			</td>
			<td data-emptyvalue="">
				<xsl:value-of select="xbrlValue"/>
			</td>
			<td data-align="left" data-emptyvalue="">
				<xsl:value-of select="target/taxonomy"/>
			</td>
			<td data-align="left" data-emptyvalue="">
				<xsl:for-each select="target/member">
					<xsl:value-of select="."/>
					<xsl:if test="not(position() = last())">
						<xsl:text>,</xsl:text>
					</xsl:if>
				</xsl:for-each>
			</td>
			<td data-align="left" data-emptyvalue="">
				<xsl:for-each select="target/dimension">
					<xsl:value-of select="@dimension"/>
					<xsl:text>=</xsl:text>
					<xsl:for-each select="member">
						<xsl:value-of select="."/>
						<xsl:if test="not(position() = last())">
							<xsl:text>;</xsl:text>
						</xsl:if>
					</xsl:for-each>
					<xsl:if test="not(position() = last())">
						<xsl:text>,</xsl:text>
					</xsl:if>
				</xsl:for-each>
			</td>
			<td data-align="left" data-emptyvalue="">
				<xsl:value-of select="@error"/>
			</td>
		</tr>
	</xsl:template>


</xsl:stylesheet>
