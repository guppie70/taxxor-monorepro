<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">

	<xsl:output method="xml" omit-xml-declaration="yes" indent="no" encoding="UTF-8"/>

	<xsl:template match="@* | * | processing-instruction() | comment()">
		<xsl:copy>
			<xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Cleanup unnecessary span elements that sometimes seem to appear in running text -->
	<xsl:template match="p[not(@class = 'chkbox')]/span[not(@*)]">
		<xsl:apply-templates/>
	</xsl:template>
	<xsl:template match="strong/span[not(@*)]">
		<xsl:apply-templates/>
	</xsl:template>
	<xsl:template match="b/span[not(@*)]">
		<xsl:apply-templates/>
	</xsl:template>
	<xsl:template match="em/span[not(@*)]">
		<xsl:apply-templates/>
	</xsl:template>
	<xsl:template match="i/span[not(@*)]">
		<xsl:apply-templates/>
	</xsl:template>
	<xsl:template match="sup/span[not(@*)]">
		<xsl:apply-templates/>
	</xsl:template>
	<xsl:template match="sub/span[not(@*)]">
		<xsl:apply-templates/>
	</xsl:template>
	<xsl:template match="a/span[not(@*)]">
		<xsl:apply-templates/>
	</xsl:template>
	<xsl:template match="li/span[not(@*)]">
		<xsl:apply-templates/>
	</xsl:template>
	
	<xsl:template match="h1/span[not(@*)]">
		<xsl:apply-templates/>
	</xsl:template>
	<xsl:template match="h2/span[not(@*)]">
		<xsl:apply-templates/>
	</xsl:template>
	<xsl:template match="h3/span[not(@*)]">
		<xsl:apply-templates/>
	</xsl:template>
	<xsl:template match="h4/span[not(@*)]">
		<xsl:apply-templates/>
	</xsl:template>
	<xsl:template match="h5/span[not(@*)]">
		<xsl:apply-templates/>
	</xsl:template>
	<xsl:template match="h6/span[not(@*)]">
		<xsl:apply-templates/>
	</xsl:template>



	<!-- Remove empty illustration wrapper divs -->
	<xsl:template match="div[contains(@class, 'illustration') and not(*)]"/>

	<!-- Remove style attributes that seem to be left overs from copy-paste -->
	<xsl:template match="
		h1[@style] | 
		h2[@style] | 
		h3[@style] | 
		h4[@style] | 
		h5[@style] | 
		h6[@style] | 
		p[@style] | 
		strong[@style] | 
		span[@style] | 
		ul[@style] | 
		i[@style] | 
		em[@style and not(@data-link-error)] | 
		i[@style] | 
		b[@style] | 
		li[@style] | 
		a[@style] | 
		sup[@style] | 
		sub[@style]
		">
		<xsl:element name="{local-name()}">
			<xsl:copy-of select="@*[not(local-name() = 'style')]"/>
			<xsl:apply-templates/>
		</xsl:element>

	</xsl:template>

	<!-- Remove empty elements -->
	<xsl:template match="h2[not(*) and (not(normalize-space()) or string-length(translate(., ' &#9;&#xA;&#xD;&#xA0;','')) = 0) and not(@*)]"/>
	<xsl:template match="h3[not(*) and (not(normalize-space()) or string-length(translate(., ' &#9;&#xA;&#xD;&#xA0;','')) = 0) and not(@*)]"/>
	<xsl:template match="h4[not(*) and (not(normalize-space()) or string-length(translate(., ' &#9;&#xA;&#xD;&#xA0;','')) = 0) and not(@*)]"/>
	<xsl:template match="h5[not(*) and (not(normalize-space()) or string-length(translate(., ' &#9;&#xA;&#xD;&#xA0;','')) = 0) and not(@*)]"/>
	<xsl:template match="h6[not(*) and (not(normalize-space()) or string-length(translate(., ' &#9;&#xA;&#xD;&#xA0;','')) = 0) and not(@*)]"/>
	<xsl:template match="p[not(*) and (not(normalize-space()) or string-length(translate(., ' &#9;&#xA;&#xD;&#xA0;','')) = 0) and not(@*)]"/>
	<xsl:template match="em[not(*) and (not(normalize-space()) or string-length(translate(., ' &#9;&#xA;&#xD;&#xA0;','')) = 0) and not(@*)]"/>
	<xsl:template match="a[not(*) and (not(normalize-space()) or string-length(translate(., ' &#9;&#xA;&#xD;&#xA0;','')) = 0) and not(@*)]"/>
	<xsl:template match="sup[not(*) and (not(normalize-space()) or string-length(translate(., ' &#9;&#xA;&#xD;&#xA0;','')) = 0) and not(@*)]"/>
	<xsl:template match="sub[not(*) and (not(normalize-space()) or string-length(translate(., ' &#9;&#xA;&#xD;&#xA0;','')) = 0) and not(@*)]"/>
	<xsl:template match="ul[not(*) and (not(normalize-space()) or string-length(translate(., ' &#9;&#xA;&#xD;&#xA0;','')) = 0) and not(@*)]"/>
	<xsl:template match="b[not(*) and (not(normalize-space()) or string-length(translate(., ' &#9;&#xA;&#xD;&#xA0;','')) = 0) and not(@*)]"/>
	<xsl:template match="strong[not(*) and (not(normalize-space()) or string-length(translate(., ' &#9;&#xA;&#xD;&#xA0;','')) = 0) and not(@*)]"/>
	<xsl:template match="i[not(*) and (not(normalize-space()) or string-length(translate(., ' &#9;&#xA;&#xD;&#xA0;','')) = 0) and not(@*)]"/>

	<!-- Remove images without a sensible src attribute -->
	<xsl:template match="img[not(@src) or string-length(normalize-space(@src)) = 0]"/>

	<!-- Remove links without a sensible href attribute -->
	<xsl:template match="a[not(@href) or string-length(normalize-space(@href)) = 0]"/>
	
	<!-- Remove SDE's without any mapping 
	<xsl:template match="span[@data-fact-id and @data-nosdsmapping='true']">
		<xsl:apply-templates/>
	</xsl:template>
	-->

</xsl:stylesheet>
