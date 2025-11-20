<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
	<xsl:param name="page-title"/>
	<xsl:param name="inline-css"/>
	<xsl:param name="lang">en</xsl:param>
	<xsl:param name="renderengine">prince</xsl:param>
	<xsl:param name="regulatorid"/>
	<xsl:param name="commentcontents"/>
	<xsl:param name="signature-marks">no</xsl:param>

	<xsl:output method="xml" omit-xml-declaration="yes" indent="no" encoding="UTF-8"/>

	<xsl:template match="@* | * | processing-instruction() | comment()">
		<xsl:copy>
			<xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
		</xsl:copy>
	</xsl:template>

	<xsl:template match="/">
		<xsl:choose>
			<xsl:when test="$regulatorid = 'sec'">
				<html lang="{$lang}">
					<head>
						<meta http-equiv="Content-Type" content="text/html;charset=UTF-8">
							<xsl:text/>
						</meta>
						<xsl:if test="string-length($commentcontents) >0">
							<xsl:comment><xsl:value-of select="$commentcontents"/></xsl:comment>
						</xsl:if>
						<title>
							<xsl:value-of select="$page-title"/>
						</title>
						<xsl:if test="string-length(normalize-space($inline-css)) > 0">
							<style>
								<xsl:value-of select="$inline-css"/>
							</style>
						</xsl:if>
					</head>
					<body>
						<xsl:attribute name="class">
							<xsl:text>full-html report-format</xsl:text>
							<xsl:if test="$signature-marks = 'no' or $signature-marks = 'false'">
								<xsl:text> hide-signature-marks</xsl:text>
							</xsl:if>
						</xsl:attribute>


						<!--				<xsl:comment>
					page-title: <xsl:value-of select="$page-title"/>
					report-type-id: <xsl:value-of select="$report-type-id"/>
					report-requirement-scheme: <xsl:value-of select="$report-requirement-scheme"/>
				</xsl:comment>-->
						<div class="main-wrapper">
							<content>
								<xsl:apply-templates select="//article[not(@data-hierarchical-level='0')]"/>
							</content>
						</div>
					</body>
				</html>
			</xsl:when>
			<xsl:otherwise>
				<html lang="{$lang}">
					<head>
						<meta http-equiv="Content-Type" content="text/html;charset=UTF-8">
							<xsl:text/>
						</meta>
						<xsl:if test="string-length($commentcontents) >0">
							<xsl:comment><xsl:value-of select="$commentcontents"/></xsl:comment>
						</xsl:if>
						<title>
							<xsl:value-of select="$page-title"/>
						</title>

						<xsl:choose>
							<xsl:when test="$renderengine = 'prince'">
								<xsl:if test="string-length(normalize-space($inline-css)) > 0">
									<style type="text/css">
										<xsl:text disable-output-escaping="yes">&lt;![CDATA[</xsl:text>
										<xsl:value-of select="$inline-css" disable-output-escaping="yes"/>
										<xsl:text disable-output-escaping="yes">]]&gt;</xsl:text>
									</style>
								</xsl:if>
							</xsl:when>
							<xsl:when test="$renderengine = 'pagedjs'">
								<xsl:copy-of select="html/head/*[not(local-name() = 'meta' or local-name() = 'title')]"/>
							</xsl:when>
							<xsl:otherwise>
								<xsl:comment>Render engine <xsl:value-of select="$renderengine"/> not supported yet...</xsl:comment>
							</xsl:otherwise>
						</xsl:choose>
					</head>
					<body>
						<xsl:attribute name="class">
							<xsl:text>full-html report-format</xsl:text>
							<xsl:if test="$signature-marks = 'no' or $signature-marks = 'false'">
								<xsl:text> hide-signature-marks</xsl:text>
							</xsl:if>
						</xsl:attribute>
						<!--				<xsl:comment>
					page-title: <xsl:value-of select="$page-title"/>
					report-type-id: <xsl:value-of select="$report-type-id"/>
					report-requirement-scheme: <xsl:value-of select="$report-requirement-scheme"/>
				</xsl:comment>-->

						<xsl:choose>
							<xsl:when test="$renderengine = 'prince'">
								<div class="main-wrapper">
									<content>
										<xsl:apply-templates select="//article[not(@data-hierarchical-level='0')]"/>
									</content>
								</div>
							</xsl:when>
							<xsl:when test="$renderengine = 'pagedjs'">
								<xsl:apply-templates select="html/body/*"/>
							</xsl:when>
							<xsl:otherwise>
								<xsl:comment>Render engine <xsl:value-of select="$renderengine"/> not supported yet...</xsl:comment>
							</xsl:otherwise>
						</xsl:choose>
					</body>
				</html>
			</xsl:otherwise>
		</xsl:choose>

	</xsl:template>


</xsl:stylesheet>
