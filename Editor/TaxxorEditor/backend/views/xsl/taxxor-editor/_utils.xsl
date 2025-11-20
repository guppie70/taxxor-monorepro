<?xml version="1.0"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<xsl:variable name="lowercase" select="'abcdefghijklmnopqrstuvwxyz'"/>
	<xsl:variable name="uppercase" select="'ABCDEFGHIJKLMNOPQRSTUVWXYZ'"/>

	<xsl:template name="get-localized-value-by-key">
		<xsl:param name="doc-translations"/>
		<xsl:param name="lang">en</xsl:param>
		<xsl:param name="id"/>
		<xsl:param name="case">nochange</xsl:param>

		<xsl:variable name="translation">
			<xsl:if test="$doc-translations">
				<xsl:value-of select="$doc-translations/configuration/localizations/translations/textfragment[@id = $id]/value[@lang = $lang]"/>
			</xsl:if>
		</xsl:variable>

		<xsl:variable name="translation-alternative">
			<xsl:if test="$doc-translations">
				<xsl:value-of select="$doc-translations/configuration/customizations/translations/textfragment[@id = $id]/value[@lang = $lang]"/>
			</xsl:if>
		</xsl:variable>

		<xsl:choose>
			<xsl:when test="string-length($translation-alternative) &gt; 0">
				<xsl:choose>
					<xsl:when test="$case = 'lower'">
						<xsl:value-of select="translate($translation-alternative, $uppercase, $lowercase)"/>
					</xsl:when>
					<xsl:when test="$case = 'upper'">
						<xsl:value-of select="translate($translation-alternative, $lowercase, $uppercase)"/>
					</xsl:when>
					<xsl:otherwise>
						<xsl:value-of select="$translation-alternative"/>
					</xsl:otherwise>
				</xsl:choose>
			</xsl:when>
			<xsl:when test="string-length($translation) &gt; 0">
				<xsl:choose>
					<xsl:when test="$case = 'lower'">
						<xsl:value-of select="translate($translation, $uppercase, $lowercase)"/>
					</xsl:when>
					<xsl:when test="$case = 'upper'">
						<xsl:value-of select="translate($translation, $lowercase, $uppercase)"/>
					</xsl:when>
					<xsl:otherwise>
						<xsl:value-of select="$translation"/>
					</xsl:otherwise>
				</xsl:choose>
			</xsl:when>
			<xsl:otherwise>
				<xsl:text>[</xsl:text>
				<xsl:value-of select="$id"/>
				<xsl:text>]</xsl:text>
			</xsl:otherwise>
		</xsl:choose>

	</xsl:template>


	<xsl:template name="string-replace-all">
		<xsl:param name="text"/>
		<xsl:param name="replace"/>
		<xsl:param name="by"/>
		<xsl:choose>
			<xsl:when test="contains($text, $replace)">
				<xsl:value-of select="substring-before($text, $replace)"/>
				<xsl:value-of select="$by"/>
				<xsl:call-template name="string-replace-all">
					<xsl:with-param name="text" select="substring-after($text, $replace)"/>
					<xsl:with-param name="replace" select="$replace"/>
					<xsl:with-param name="by" select="$by"/>
				</xsl:call-template>
			</xsl:when>
			<xsl:otherwise>
				<xsl:value-of select="$text"/>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>
	
	<xsl:template name="render-querystring-taxxor-pages">
		<xsl:param name="doc-configuration"/>
		<xsl:param name="projectId"/>
		<xsl:param name="editorId"/>
		<xsl:param name="sectionId"/>
		
		<xsl:text>&amp;vid=</xsl:text>
		<xsl:value-of select="$doc-configuration/configuration/cms_projects/cms_project[@id = $projectId]/versions/version[position() = last()]/@id"/>
		<xsl:text>&amp;did=</xsl:text>
		<xsl:value-of select="$sectionId"/>
		<xsl:text>&amp;ctype=</xsl:text>
		<xsl:value-of select="$doc-configuration/configuration/cms_projects/cms_project[@id = $projectId]/content_types/content_management/type/@id"/>
		<xsl:text>&amp;rtype=</xsl:text>
		<xsl:value-of select="$doc-configuration/configuration/cms_projects/cms_project[@id = $projectId]/@report-type"/>
		<xsl:text>&amp;octype=</xsl:text>
		<xsl:value-of select="$doc-configuration/configuration/editors/editor[@id = $editorId]/output_channels/output_channel/@type"/>
		<xsl:text>&amp;ocvariantid=</xsl:text>
		<xsl:value-of select="$doc-configuration/configuration/editors/editor[@id = $editorId]/output_channels/output_channel/variants/variant/@id"/>
		<xsl:text>&amp;oclang=</xsl:text>
		<xsl:value-of select="$doc-configuration/configuration/editors/editor[@id = $editorId]/output_channels/output_channel/variants/variant/@lang"/>
		
	</xsl:template>

</xsl:stylesheet>
