<?xml version='1.0'?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

	<xsl:output method="html" omit-xml-declaration="yes"/>
	<xsl:strip-space elements="*"/>


	<xsl:template match="/">
		<ul class="root">
			<xsl:apply-templates select="/items/structured/item"/>
		</ul>
	</xsl:template>


	<xsl:template match="item">
		<xsl:param name="level">0</xsl:param>
		<xsl:variable name="next_level">
			<xsl:value-of select="number($level) + 1"/>
		</xsl:variable>

		<li id="lid-{@id}" class="level-{$level}">
			<input type="checkbox" id="inputid-{@id}" onclick="sel(event, this, 'inputid-{@id}')"/>
			<xsl:value-of select="web_page/linkname"/>
			
			<xsl:if test="./sub_items">
				<ul>
					<xsl:apply-templates select="./sub_items/item[not(@hidefromui = 'true')]">
						<xsl:with-param name="level" select="$next_level"/>
					</xsl:apply-templates>
				</ul>
			</xsl:if>
		</li>
	</xsl:template>

</xsl:stylesheet>
