<?xml version="1.0"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<!-- base includes -->
	<xsl:include href="_utils.xsl"/>

	<xsl:param name="doc-configuration"/>
	<xsl:param name="pageId">cms-overview</xsl:param>
	<xsl:param name="projectId"/>
	<xsl:param name="editorId"/>


	<xsl:variable name="current-depth">
		<xsl:value-of select="count(//item[@id=$pageId]/ancestor-or-self::item)"/>
	</xsl:variable>

	<xsl:output method="html" omit-xml-declaration="yes" indent="yes"/>


	<xsl:template match="/">
		<!--
		<xsl:text>current depth: </xsl:text>
		<xsl:value-of select="$current-depth"/>
		-->
		<ul class="nav nav-list">
			<xsl:apply-templates select="/items/structured/item">
				<xsl:with-param name="level">0</xsl:with-param>
			</xsl:apply-templates>
		</ul>
	</xsl:template>


	<xsl:template match="item">
		<xsl:param name="level"/>

		<xsl:variable name="item-id" select="@id"/>

		<xsl:choose>
			<!-- sometimes we do not want to display an item -->
			<xsl:when test="@hidefromui='true'"/>
			<xsl:when test="$current-depth=1 and @id='cms_project-details'"/>
			<xsl:when test="$current-depth=2 and @id='cms_content-editor'"/>
			<xsl:when test="$current-depth=3 and @id='cms_content-editor' and not($pageId=@id)"/>
			<xsl:when test="$doc-configuration/configuration/editors/editor[@id=$editorId]/disable/hierarchy/item[@id=$item-id]"/>
			<xsl:otherwise>
				<li id="{@id}">
					<xsl:attribute name="class">
						<xsl:choose>
							<xsl:when test="@id=$pageId">
								<xsl:text>active</xsl:text>
							</xsl:when>
							<xsl:when test="sub_items//item/@id=$pageId and not(@id='cms-overview')">
								<xsl:text>active open</xsl:text>
							</xsl:when>
						</xsl:choose>
					</xsl:attribute>

					<a href="javascript:navigate('{web_page/path}');">
						<i>
							<xsl:call-template name="apply-icon-class">
								<xsl:with-param name="page-id" select="@id"/>
								<xsl:with-param name="level" select="$level"/>
							</xsl:call-template>
						</i>
						<span class="menu-text">
							<xsl:value-of select="web_page/linkname"/>
						</span>
					</a>
					<b class="arrow"/>
					<xsl:if test="number($level) &gt; 0 and $current-depth &gt; 1">
						<xsl:apply-templates select="sub_items">
							<xsl:with-param name="level" select="$level"/>
						</xsl:apply-templates>
					</xsl:if>
				</li>
			</xsl:otherwise>
		</xsl:choose>



		<xsl:if test="number($level) = 0">
			<xsl:apply-templates select="sub_items/item">
				<xsl:with-param name="level" select="number($level) + 1"/>
			</xsl:apply-templates>
		</xsl:if>

	</xsl:template>

	<xsl:template match="sub_items">
		<xsl:param name="level"/>

		<ul class="submenu">
			<xsl:apply-templates select="item[not(@hidefromui='true')]">
				<xsl:with-param name="level" select="number($level) + 1"/>
			</xsl:apply-templates>
		</ul>

	</xsl:template>

	<xsl:template name="apply-icon-class">
		<xsl:param name="page-id"/>
		<xsl:param name="level"/>

		<xsl:if test="$level &lt; 2">
			<xsl:attribute name="class">
				<xsl:text>menu-icon</xsl:text>
				<xsl:choose>
					<xsl:when test="$page-id='cms-overview'">
						<xsl:text> fa fa-tachometer</xsl:text>
					</xsl:when>
					<xsl:when test="$page-id='cms_user-settings'">
						<xsl:text> fa fa-cog</xsl:text>
					</xsl:when>
					<xsl:when test="$page-id='cms_user-profile'">
						<xsl:text> fa fa-user</xsl:text>
					</xsl:when>
					<xsl:when test="$page-id='cms_about'">
						<xsl:text> fa fa-info</xsl:text>
					</xsl:when>
					<xsl:when test="$page-id='cms_system-administration'">
						<xsl:text> fa fa-cogs</xsl:text>
					</xsl:when>

					<xsl:otherwise>
						<xsl:text> fa fa-file-o</xsl:text>
					</xsl:otherwise>
				</xsl:choose>

			</xsl:attribute>
		</xsl:if>

		<xsl:if test="$level &gt; 1 and $pageId=$page-id">
			<xsl:attribute name="class">
				<xsl:text>menu-icon fa fa-caret-right</xsl:text>
			</xsl:attribute>
		</xsl:if>

	</xsl:template>


</xsl:stylesheet>
