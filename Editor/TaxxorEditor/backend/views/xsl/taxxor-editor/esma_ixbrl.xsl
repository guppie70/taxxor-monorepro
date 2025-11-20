<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:param name="report-type-id"/>
	<xsl:param name="report-requirement-scheme"/>
	<xsl:param name="lang">en</xsl:param>

	<xsl:output method="xml" omit-xml-declaration="yes" indent="no" encoding="UTF-8" cdata-section-elements="stylefoo"/>

	<xsl:template match="@* | * | processing-instruction() | comment()">
		<xsl:copy>
			<xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="html">
		<html xml:lang="{$lang}">
			<xsl:apply-templates/>
		</html>
	</xsl:template>


	<xsl:template match="article">
		<div data-originalnodename="{local-name()}" class="article">
			<xsl:copy-of select="@*[not(contains(local-name(), 'tangelo') or local-name() = 'modified-by' or local-name() = 'lang')]"/>
			<xsl:if test="contains(@id, 'cover')">
				<xsl:attribute name="class">
					<xsl:text>cover</xsl:text>
					<xsl:choose>
						<xsl:when test="contains(@id, 'front')"> front</xsl:when>
						<xsl:otherwise> back</xsl:otherwise>
					</xsl:choose>
				</xsl:attribute>
			</xsl:if>
			<xsl:apply-templates/>
		</div>
	</xsl:template>

	<xsl:template match="header">
		<div data-originalnodename="{local-name()}" class="header">
			<xsl:copy-of select="@*[local-name() = 'id' or local-name() = 'style']"/>
			<xsl:apply-templates/>
		</div>
	</xsl:template>



	<!--
	<xsl:template match="div[contains(@class, 'pageblock')]">
		<xsl:choose>
			<xsl:when test="$report-type-id = 'philips-quarterly-report'">
				<!-\- Retain the wrapper div -\->
				<div>
					<xsl:copy-of select="@*"/>
					<xsl:apply-templates/>
				</div>
			</xsl:when>
			<xsl:when test="contains(@class, 'bluebox')">
				<!-\- Retain the wrapper div -\->
				<div>
					<xsl:copy-of select="@*"/>
					<xsl:apply-templates/>
				</div>
			</xsl:when>
			<xsl:otherwise>
				<!-\- Remove the wrapper div -\->
				<xsl:apply-templates/>
			</xsl:otherwise>
		</xsl:choose>
		
	</xsl:template>
	-->

	<xsl:template match="div[contains(@class, 'illustration')]">
		<div>
			<xsl:copy-of select="@*[local-name() = 'id' or local-name() = 'class' or local-name() = 'style']"/>
			<xsl:apply-templates/>
		</div>
	</xsl:template>

	<xsl:template match="div[@class = 'intext-footnote-wrapper' and count(*) = 0]"/>


	<xsl:template match="div[@class = 'page-break']"/>

	<xsl:template match="section">
		<xsl:choose>
			<xsl:when test="$report-type-id = 'philips-quarterly-report'">
				<!-- Retain the section node, but translate to a wrapper div -->
				<div>
					<xsl:copy-of select="@*[local-name() = 'id' or local-name() = 'class' or local-name() = 'style']"/>
					<xsl:apply-templates/>
				</div>
			</xsl:when>
			<xsl:otherwise>
				<!-- Remove the section node completely -->
				<xsl:apply-templates/>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>

	<xsl:template match="thead[count(*) = 0]"/>

	<xsl:template match="a[@target]">
		<a>
			<xsl:copy-of select="@*[not(local-name() = 'target')]"/>
			<xsl:apply-templates/>
		</a>
	</xsl:template>

	<xsl:template match="a[contains(@href, '/') or contains(@href, 'mailto')]">
		<xsl:apply-templates/>
	</xsl:template>

	<xsl:template match="br">
		<br/>
	</xsl:template>

	<xsl:template match="p[@align or @guid]">
		<p>
			<xsl:copy-of select="@*[not(local-name() = 'align' or local-name() = 'guid')]"/>
			<xsl:apply-templates/>
		</p>
	</xsl:template>

	<xsl:template match="*[@guid]">
		<xsl:element name="{local-name()}">
			<xsl:copy-of select="@*[not(local-name() = 'guid')]"/>
			<xsl:apply-templates/>
		</xsl:element>
	</xsl:template>

	<xsl:template match="ul[@id]">
		<ul>
			<xsl:copy-of select="@*[not(local-name() = 'id')]"/>
			<xsl:apply-templates/>
		</ul>
	</xsl:template>

	<xsl:template match="div[@class = 'pageblock quote']/div[@class = 'text']">
		<div>
			<xsl:copy-of select="@*"/>
			<xsl:text>"</xsl:text>
			<xsl:apply-templates/>
			<xsl:text>"</xsl:text>
		</div>
	</xsl:template>

	<!-- fix for footnotes containing nested span's for the footnote text -->
	<xsl:template match="div[@class = 'footnote' and span/span and not(span/span/@data-fact-id)]">
		<div>
			<xsl:copy-of select="@*"/>
			<xsl:apply-templates select="*[not(local-name() = 'span')]"/>
			<xsl:apply-templates select="span/span"/>
		</div>
	</xsl:template>

	<xsl:template match="img[not(@alt)]">
		<xsl:variable name="alt-text">
			<xsl:choose>
				<xsl:when test="@data-originalvalue-src">
					<xsl:value-of select="substring-after(substring-before(@data-originalvalue-src, '.'), '/images/')"/>

				</xsl:when>
				<xsl:otherwise>
					<xsl:text>Visual</xsl:text>
				</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>

		<img alt="{$alt-text}">
			<xsl:copy-of select="@*[not(contains(local-name(), 'data-'))]"/>
		</img>
	</xsl:template>

	<xsl:template match="@contenteditable"/>

	<xsl:template match="@top-bottom-lines"/>

	<xsl:template match="*[@data-remove]"/>

	<xsl:template match="*[@data-passlayout = 'landscape']"/>

	<xsl:template match="div[contains(@class, 'c-table') and not(@data-showheaderdates = 'true')]/div/p[contains(@class, 'datatitle')]"/>


	<xsl:template match="style[@id='documentstylesincharacterdata']">
		<style id="documentstyles">
			<xsl:copy-of select="@*[not(local-name() = 'id')]"/>
			<xsl:text disable-output-escaping="yes">&lt;![CDATA[</xsl:text>
			<xsl:value-of disable-output-escaping="yes" select="."/>
			<xsl:text disable-output-escaping="yes">]]&gt;</xsl:text>   
        </style>
	</xsl:template>


</xsl:stylesheet>
