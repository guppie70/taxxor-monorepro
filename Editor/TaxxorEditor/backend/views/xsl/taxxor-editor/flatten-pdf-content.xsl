<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:output method="xml" omit-xml-declaration="yes" indent="yes" encoding="UTF-8"/>
	<xsl:variable name="removedsectionmethod" select="/*/@removedsectionmethod"/>
	<xsl:variable name="nodelist-articles" select="//article"/>
	<xsl:variable name="initial-hierarchical-level" select="//article[1]/@data-hierarchical-level"/>
	
	<xsl:template match="@* | node()">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()"/>
		</xsl:copy>
	</xsl:template>
	
	<xsl:template match="metadata"/>
	
	<xsl:template match="article[@data-hierarchical-level]">

		<xsl:choose>
			<xsl:when test="contains(@class, 'del') and $removedsectionmethod='simple'">
				<article id="{@id}">
					<xsl:apply-templates select="@*[contains(local-name(), 'data-') or local-name() = 'class']"/>
					<xsl:apply-templates/>
				</article>
			</xsl:when>
			
			<xsl:when test="number(@data-hierarchical-level) = number($initial-hierarchical-level)">
				<article id="{@id}">
					<xsl:apply-templates select="@*[contains(local-name(), 'data-') or local-name() = 'class']"/>
					<!--<xsl:comment>
					initial-hierarchical-level: <xsl:value-of select="$initial-hierarchical-level"/>
					</xsl:comment>-->
				
					<xsl:apply-templates/>
					
					<xsl:if test="@data-siblinginsert-start">
						<xsl:variable name="insert-start" select="number(@data-siblinginsert-start)"/>
						<xsl:choose>
							<xsl:when test="@data-siblinginsert-end">
								<xsl:variable name="insert-end" select="number(@data-siblinginsert-end)"/>
								<xsl:for-each select="$nodelist-articles[(position() >= $insert-start) and (position() &lt;= $insert-end)]">
									<xsl:if test="not(contains(./@class, 'del') or ./@data-articletype='customdevider')">
										<xsl:apply-templates/>
										<!--<xsl:comment><xsl:value-of select="@id"/></xsl:comment>-->
									</xsl:if>
								</xsl:for-each>
							</xsl:when>
							<xsl:otherwise>
								<xsl:for-each select="$nodelist-articles[position() >= $insert-start]">
									<xsl:if test="not(contains(./@class, 'del') or ./@data-articletype='customdevider')">
										<xsl:apply-templates/>
									</xsl:if>
								</xsl:for-each>
							</xsl:otherwise>
						</xsl:choose>
					</xsl:if>
				</article>
			</xsl:when>
		</xsl:choose>

	</xsl:template>
	
	
	<xsl:template match="article/section">
		<xsl:apply-templates/>
	</xsl:template>
</xsl:stylesheet>
