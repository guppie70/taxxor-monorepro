<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:param name="language1"></xsl:param>
	<xsl:param name="language2"></xsl:param>
	<xsl:param name="language3"></xsl:param>
	<xsl:param name="language4"></xsl:param>



	<xsl:output method="xml" omit-xml-declaration="yes" indent="no" encoding="UTF-8"/>

	<xsl:template match="@* | * | processing-instruction() | comment()">
		<xsl:copy>
			<xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Makes sure that for each language, there is a value entry present -->
	<xsl:template match="element">
		<element>
			<xsl:copy-of select="@*"/>
			<xsl:choose>
				<xsl:when test="string-length($language4) = 2 and count(./value) &lt; 4">
					<xsl:apply-templates/>
					<xsl:if test="not(value[@lang = $language2])">
						<!--<xsl:comment>Missing value for <xsl:value-of select="$language2"/></xsl:comment>-->
						<value lang="{$language2}">
							<xsl:apply-templates select="value[1]/node()"/>
						</value>
					</xsl:if>
					<xsl:if test="not(value[@lang = $language3])">
						<!--<xsl:comment>Missing value for <xsl:value-of select="$language3"/></xsl:comment>-->
						<value lang="{$language3}">
							<xsl:apply-templates select="value[1]/node()"/>
						</value>
					</xsl:if>
					<xsl:if test="not(value[@lang = $language4])">
						<!--<xsl:comment>Missing value for <xsl:value-of select="$language4"/></xsl:comment>-->
						<value lang="{$language4}">
							<xsl:apply-templates select="value[1]/node()"/>
						</value>
					</xsl:if>
				</xsl:when>

				<xsl:when test="string-length($language3) = 2 and count(./value) &lt; 3">
					<xsl:apply-templates/>
					<xsl:if test="not(value[@lang = $language2])">
						<!--<xsl:comment>Missing value for <xsl:value-of select="$language2"/></xsl:comment>-->
						<value lang="{$language2}">
							<xsl:apply-templates select="value[1]/node()"/>
						</value>
					</xsl:if>
					<xsl:if test="not(value[@lang = $language3])">
						<!--<xsl:comment>Missing value for <xsl:value-of select="$language3"/></xsl:comment>-->
						<value lang="{$language3}">
							<xsl:apply-templates select="value[1]/node()"/>
						</value>
					</xsl:if>
				</xsl:when>
				<xsl:when test="string-length($language2) = 2 and count(value) &lt; 2">
					<xsl:apply-templates/>
					<value lang="{$language2}">
						<xsl:apply-templates select="value/node()"/>
					</value>
				</xsl:when>
				<xsl:otherwise>
					<xsl:apply-templates select="value"/>
				</xsl:otherwise>
			</xsl:choose>
		</element>
	</xsl:template>

	<!-- Used to convert 'old style' SDE cache into the new style that contains a value for each language -->
	<xsl:template match="element[not(value/@lang or value/@lang = '')]">
		<element>
			<xsl:copy-of select="@*"/>
			<value lang="{$language1}">
				<xsl:apply-templates select="value/node()"/>
			</value>

			<xsl:if test="string-length($language2) = 2">
				<value lang="{$language2}">
					<xsl:apply-templates select="value/node()"/>
				</value>
			</xsl:if>
		</element>
	</xsl:template>



</xsl:stylesheet>
