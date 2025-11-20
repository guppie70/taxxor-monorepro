<?xml version='1.0'?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	
	<xsl:output method="html" omit-xml-declaration="yes"/>
	
	<xsl:variable name="newline">
		<xsl:text>
		</xsl:text>
	</xsl:variable>
	
	<xsl:template match="/">
		<xsl:apply-templates select="/items/structured/item"/>	
	</xsl:template>
	
	<xsl:template match="item">
		<xsl:param name="level">0</xsl:param>
		<xsl:variable name="next_level"><xsl:value-of select="number($level) + 1"/></xsl:variable>
		<dl>
			
			<dd>
				<xsl:if test="number($level)=0">
				<xsl:attribute name="style">margin-left:0px;</xsl:attribute>
				</xsl:if>
				<div id="item_{@id}" class="item_level{$level}">
					<!--<xsl:value-of select="@editable"/>-->
					<xsl:choose>
						<xsl:when test="string(@editable)='true'"><a href="javascript:loadXmlByItemId('{@id}')"><xsl:value-of select="web_page/linkname"/></a></xsl:when>
						<xsl:otherwise><xsl:value-of select="web_page/linkname"/></xsl:otherwise>
					</xsl:choose>
				
					<div class="item_data">
						<span class="item_attributes">
							<xsl:for-each select="@*">
								<span>
									<xsl:attribute name="class">
										<xsl:value-of select="local-name(.)"/>
									</xsl:attribute>
									<xsl:value-of select="."/>
								</span>
							</xsl:for-each>
						</span>
						<span class="system">
							<span class="data_src">
								<xsl:if test="system/data">
									<xsl:value-of select="system/data/@src"/>
								</xsl:if>
							</span>
							<span class="xsl_id">
								<xsl:if test="system/xsl">
									<xsl:value-of select="system/xsl/@id"/>
								</xsl:if>
							</span>
						</span>
						
					</div>
				</div>
		
			
				<xsl:if test="./sub_items">
					<xsl:apply-templates select="./sub_items/item[not(@hidefromui='true')]">
						<xsl:with-param name="level" select="$next_level"/>				
					</xsl:apply-templates>					
				</xsl:if>
			</dd>
		</dl>
	</xsl:template>
	
</xsl:stylesheet>
