<?xml version='1.0'?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	
	<xsl:param name="lang">en</xsl:param>
	<xsl:param name="regex-class-search"></xsl:param>

	
	<xsl:output method="xml" indent="no" encoding="UTF-8" omit-xml-declaration="yes"/>
	
	<!-- Idenitity copy stylesheet -->
	<xsl:template match="@* | node()">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()"/>
		</xsl:copy>
	</xsl:template>
	
	<!-- Assure that we only transform content for a specific language -->
	<xsl:template match="/">
		<data>
			<xsl:copy-of select="@*"/>
			<!--		
			<xsl:comment>
				regex-class-search: <xsl:value-of select="$regex-class-search"/>
			</xsl:comment>
			-->			
			<xsl:copy-of select="data/system"/>
			<xsl:choose>
				<xsl:when test="$lang = 'all'">
					<xsl:apply-templates select="data/*[not(local-name() = 'system')]"/>
				</xsl:when>
				<xsl:otherwise>
					<xsl:for-each select="data/*[not(local-name() = 'system')]">
						<xsl:choose>
							<xsl:when test="local-name() = 'content' and @lang = $lang">
								<xsl:apply-templates select="."/>
							</xsl:when>
							<xsl:otherwise>
								<xsl:copy-of select="."/>
							</xsl:otherwise>
						</xsl:choose>
					</xsl:for-each>
				</xsl:otherwise>
			</xsl:choose>			
		</data>
	</xsl:template>
	
	<!-- Fix classes -->
	<xsl:template match="*[@class]">
		<xsl:element name="{local-name()}">
			<xsl:copy-of select="@*[not(local-name() = 'class')]"/>
			
			
			<xsl:variable name="current-class" select="@class"/>
			
			<xsl:if test="string-length(normalize-space($current-class))>0">
				
				<xsl:choose>
					<!-- Do not remove the whitespace classes for the table wrappers which have been marked with 'span all columns' class -->
					<xsl:when test="contains($current-class, 'c-table') and contains($current-class, 'tx-cs-a')">
						<xsl:attribute name="class">
							<xsl:value-of select="$current-class"/>
						</xsl:attribute>
					</xsl:when>
					<xsl:otherwise>
						<!-- Replace the whitespace classes using a regular expression -->
						<xsl:variable name="replaced-class" select='replace($current-class, $regex-class-search, "")'/>
						
						<xsl:choose>
							<xsl:when test="string-length(normalize-space($replaced-class))>0">
								<xsl:attribute name="class">
									<xsl:value-of select="$replaced-class"/>
								</xsl:attribute>
							</xsl:when>
							<xsl:otherwise>
								<xsl:attribute name="class">
									<xsl:value-of select="$current-class"/>
								</xsl:attribute>
							</xsl:otherwise>
						</xsl:choose>
					
					</xsl:otherwise>
				</xsl:choose>
	
			</xsl:if>

			<xsl:apply-templates/>
		</xsl:element>
	</xsl:template>

</xsl:stylesheet>
