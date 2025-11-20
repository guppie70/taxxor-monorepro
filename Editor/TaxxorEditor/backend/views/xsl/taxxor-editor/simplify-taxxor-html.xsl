<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:param name="add-html-chrome">true</xsl:param>
	<xsl:param name="post-processing-format">docx</xsl:param>
	<xsl:param name="inline-css"/>
	<xsl:param name="output-channel-variant">20F</xsl:param>
	<xsl:param name="reporting-requirement-scheme"/>
	<xsl:param name="tablesonly">no</xsl:param>
	<xsl:param name="pdfrendermode">normal</xsl:param>
	<xsl:param name="footnote-suffix">)</xsl:param>


	<xsl:output method="xml" omit-xml-declaration="yes" indent="no" encoding="UTF-8"/>

	<xsl:template match="@* | * | processing-instruction() | comment()">
		<xsl:copy>
			<xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="/">
		<xsl:choose>
			<xsl:when test="$add-html-chrome = 'true'">
				<html>
					<head>
						<meta charset="utf-8"/>
						<title>
							<xsl:value-of select="$post-processing-format"/>
							<xsl:text> pre-processing and simplification result</xsl:text>
						</title>
						<xsl:if test="string-length(normalize-space($inline-css)) > 0">
							<style type="text/css">
								<xsl:value-of select="$inline-css"/>
							</style>
						</xsl:if>
					</head>
					<body>
						<xsl:apply-templates/>
					</body>
				</html>
			</xsl:when>
			<xsl:otherwise>
<!--				<xsl:comment>
					local-name: <xsl:value-of select="local-name(node())"/>
					count: <xsl:value-of select="count(node()/*)"/>
				</xsl:comment>-->
				<xsl:choose>
					<xsl:when test="not(local-name(node()) = 'html') and count(node()/*) &gt; 1">
						<content>
							<xsl:apply-templates/>
						</content>
					</xsl:when>
					<xsl:otherwise>
						<xsl:apply-templates/>
					</xsl:otherwise>
				</xsl:choose>
			</xsl:otherwise>
		</xsl:choose>

	</xsl:template>


	<!-- Strips AR / 20-F elements -->
	<xsl:template match="*[@data-outputchannel]">

		<xsl:variable name="normalized-outputchannel-variant">
			<xsl:call-template name="normalize-output-channel-variant">
				<xsl:with-param name="output-channel-variant" select="@data-outputchannel"/>
			</xsl:call-template>
		</xsl:variable>

		<xsl:choose>
			<xsl:when test="@data-outputchannel = $normalized-outputchannel-variant or @data-outputchannel = 'Both' or @data-outputchannel = 'All'">
				<xsl:element name="{local-name()}">
					<xsl:choose>
						<xsl:when test="local-name() = 'article'">
							<xsl:choose>
								<xsl:when test="$post-processing-format = 'IXBRL' or $post-processing-format = 'XBRL' or $post-processing-format = 'PDF'">
									<xsl:apply-templates select="@*[not(contains(local-name(), 'modified'))]"/>
									<xsl:apply-templates/>
								</xsl:when>
								<xsl:otherwise>
									<!-- Strip data- attributes -->
									<xsl:apply-templates select="@*[not(contains(local-name(), 'data-') or contains(local-name(), 'modified'))]"/>
									<xsl:apply-templates/>
								</xsl:otherwise>
							</xsl:choose>
						</xsl:when>
						<xsl:otherwise>
							<xsl:apply-templates/>
						</xsl:otherwise>
					</xsl:choose>

				</xsl:element>
			</xsl:when>
			<xsl:otherwise>
				<xsl:comment>Stripped from this output channel</xsl:comment>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>

	<!-- Footnotes -->
	<xsl:template match="div[@class = 'intext-footnote-wrapper']/span">
		<div>
			<xsl:copy-of select="@*"/>
			<xsl:apply-templates/>
		</div>
	</xsl:template>

	<xsl:template match="sup[@class = 'fn']">
		<sup>
			<xsl:copy-of select="@*"/>
			<xsl:apply-templates/>
			<xsl:if test="string-length($footnote-suffix) &gt; 0">
				<span class="fn-sffx">
					<xsl:value-of select="$footnote-suffix"/>
				</span>
			</xsl:if>
		</sup>
	</xsl:template>
	
	<!-- Articles for word generation -->
	<xsl:template match="article[@data-hierarchical-level and not(@data-hierarchical-level = '0')]">
		<article>
			<xsl:copy-of select="@*"/>
			<xsl:choose>
				<xsl:when test="$post-processing-format = 'docx'">
					<xsl:variable name="current-class">
						<xsl:value-of select="normalize-space(@class)"/>
					</xsl:variable>
					<xsl:attribute name="class">
						<xsl:value-of select="concat($current-class, ' hierarchical-level-', @data-hierarchical-level)"/>
					</xsl:attribute>
				</xsl:when>
			</xsl:choose>
			<xsl:apply-templates/>
		</article>
	</xsl:template>

	<!-- Nodes to strip -->
	<xsl:template match="article[@data-hierarchical-level = '0']">
		<xsl:if test="count(//article) = 1">
			<article>
				<xsl:copy-of select="@*"/>
				<xsl:apply-templates/>
			</article>
		</xsl:if>
	</xsl:template>

	<xsl:template match="div[@class = 'body-wrapper'] | div[@class = 'pageblock']">
		<xsl:apply-templates/>
	</xsl:template>

	<xsl:template match="@class">
		<xsl:choose>
			<xsl:when test="normalize-space(.) = ''"/>
			<xsl:otherwise>
				<xsl:attribute name="class">
					<xsl:value-of select="."/>
				</xsl:attribute>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>

	<xsl:template match="@guid | @data-sourcesettings | @data-name | @data-workbookreference | @contenteditable"/>

	<xsl:template match="div[@id = 'document-metadata']">
		<xsl:if test="$post-processing-format = 'IXBRL' or $post-processing-format = 'XBRL'">
			<div>
				<xsl:copy-of select="@*"/>
				<xsl:apply-templates/>
			</div>
		</xsl:if>
	</xsl:template>

	<xsl:template match="head">
		<head>
			<xsl:apply-templates/>
			<xsl:if test="string-length(normalize-space($inline-css)) > 0">
				<style type="text/css">
					<xsl:value-of select="$inline-css"/>
				</style>
			</xsl:if>
		</head>
	</xsl:template>

	<xsl:template match="span[contains(@class, 'txdynamicdate')]">
		<xsl:choose>
			<xsl:when test="$tablesonly = 'no'">
				<xsl:apply-templates/>
			</xsl:when>
			<xsl:otherwise>
				<span>
					<xsl:copy-of select="@*"/>
					<xsl:apply-templates/>
				</span>
			</xsl:otherwise>
		</xsl:choose>

	</xsl:template>

	<xsl:template match="span[@type]">
		<span>
			<xsl:copy-of select="@*[not(local-name() = 'type')]"/>
			<xsl:apply-templates/>
		</span>
	</xsl:template>

	<xsl:template match="tr[count(td | th) = 0]"/>


	<xsl:template match="div[contains(@id, 'tablewrapper_')]">
		<div>
			<xsl:choose>
				<xsl:when test="$pdfrendermode = 'diff'">
					<xsl:copy-of select="@*"/>
				</xsl:when>
				<xsl:when test="$post-processing-format = 'docx'">
					<xsl:copy-of select="@*"/>
				</xsl:when>
				<xsl:otherwise>
					<xsl:copy-of select="@*[not(local-name() = 'id')]"/>
				</xsl:otherwise>
			</xsl:choose>
			<xsl:apply-templates/>
		</div>
	</xsl:template>

	<xsl:template match="table[@id]">
		<table>
			<xsl:choose>
				<xsl:when test="$pdfrendermode = 'diff'">
					<xsl:copy-of select="@*"/>
				</xsl:when>
				<xsl:when test="$post-processing-format = 'docx'">
					<xsl:copy-of select="@*"/>
				</xsl:when>
				<xsl:otherwise>
					<xsl:copy-of select="@*[not(local-name() = 'id')]"/>
				</xsl:otherwise>
			</xsl:choose>
			<xsl:apply-templates/>
		</table>
	</xsl:template>

	<xsl:template match="table[@data-workbookreference]">
		<table>
			<xsl:choose>
				<xsl:when test="$pdfrendermode = 'diff'">
					<xsl:copy-of select="@*"/>
				</xsl:when>
				<xsl:when test="$post-processing-format = 'docx'">
					<xsl:copy-of select="@*"/>
				</xsl:when>
				<xsl:otherwise>
					<xsl:copy-of select="@*[not(local-name() = 'id')]"/>
				</xsl:otherwise>
			</xsl:choose>
			<xsl:apply-templates/>
		</table>
	</xsl:template>

	<xsl:template match="ul[ul]">
		<ul>
			<xsl:copy-of select="@*"/>

			<xsl:for-each select="*">
				<xsl:choose>
					<xsl:when test="local-name() = 'ul'">
						<li>
							<xsl:apply-templates select="."/>
						</li>
					</xsl:when>
					<xsl:otherwise>
						<xsl:apply-templates select="."/>
					</xsl:otherwise>
				</xsl:choose>
			</xsl:for-each>

		</ul>
	</xsl:template>

	<xsl:template match="span[@lang]">
		<span>
			<xsl:apply-templates/>
		</span>
	</xsl:template>

	<xsl:template match="p[@id]">
		<p>
			<xsl:copy-of select="@*[not(local-name() = 'id')]"/>
			<xsl:apply-templates/>
		</p>
	</xsl:template>

	<!-- Strips AR / 20-F elements -->
	<xsl:template match="*[contains(@class, 'data20-f')]">
		<xsl:if test="$output-channel-variant = '20F'">
			<xsl:element name="{local-name()}">
				<xsl:copy-of select="@*"/>
				<xsl:apply-templates/>
			</xsl:element>
		</xsl:if>
	</xsl:template>

	<xsl:template match="*[contains(@class, 'dataar')]">
		<xsl:if test="not($output-channel-variant = '20F')">
			<xsl:element name="{local-name()}">
				<xsl:copy-of select="@*"/>
				<xsl:apply-templates/>
			</xsl:element>
		</xsl:if>
	</xsl:template>

	<xsl:template name="normalize-output-channel-variant">
		<xsl:param name="output-channel-variant"/>
		<xsl:choose>
			<xsl:when test="$output-channel-variant = '20F'">
				<xsl:text>20-F</xsl:text>
			</xsl:when>
			<xsl:otherwise>
				<xsl:value-of select="$output-channel-variant"/>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>

</xsl:stylesheet>
