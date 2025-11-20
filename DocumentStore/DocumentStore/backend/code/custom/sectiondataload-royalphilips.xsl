<?xml version="1.0"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">

	<xsl:param name="output-channel-language"/>

	<xsl:output method="xml" omit-xml-declaration="yes" indent="no" encoding="UTF-8"/>

	<xsl:template match="@* | * | processing-instruction() | comment()">
		<xsl:copy>
			<xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
		</xsl:copy>
	</xsl:template>
	
	<!-- Make sure the default DOM structure underneath the main article node is present -->
	<xsl:template match="article[not(ancestor::main or ancestor::div) and not(div/section) and not(@data-template)]">
		<article>
			<xsl:copy-of select="@*"/>
			<div data-type="one-column" class="pageblock">
				<section class="content regular" data-name="main-content">
					<xsl:apply-templates/>
				</section>
			</div>
		</article>
	</xsl:template>

	<!-- Transform SVG object to a classical image -->
	<xsl:template match="div[contains(@class, 'illustration')]/object[@data]">
		<img alt="Illustration" class="illustration" src="{@data}"/>
	</xsl:template>
	
	<!-- Remove empty SVG wrapper elements -->
	<xsl:template match="div[contains(@class, 'illustration') and not(*)]"/>

	<!-- Remove the div that needed to be in each table cell -->
	<xsl:template match="tr[not(contains(@class, 'slanted'))]/*[div]">
		<xsl:variable name="elementname" select="local-name(.)"/>

		<xsl:element name="{$elementname}">
			<xsl:copy-of select="@*"/>
			<xsl:choose>
				<xsl:when test="count(div/p[not(@data-fact-id)]) = 1 and count(div/*) = 1">
					<!--<xsl:comment>1</xsl:comment>-->
					<xsl:apply-templates select="div/p/node()"/>
				</xsl:when>
				<xsl:when test="div and count(*) = 1">
					<!--<xsl:comment>2</xsl:comment>-->
					<xsl:apply-templates select="div/*"/>
				</xsl:when>
				<xsl:otherwise>
					<!--<xsl:comment>3</xsl:comment>-->
					<xsl:apply-templates/>
				</xsl:otherwise>
			</xsl:choose>
		</xsl:element>
	</xsl:template>

	<!-- Correct table rows with a lot of hide classes in it -->
	<xsl:template match="tr[@data-hiddenrow = 'true' and not(td)]">
		<tr>
			<xsl:copy-of select="@*[not(local-name() = 'class')]"/>
			<xsl:if test="@class">
				<xsl:attribute name="class">
					<xsl:choose>
						<xsl:when test="contains(@class, 'hide hide')">
							<xsl:value-of select="substring-before(@class, 'hide hide')"/>
							<xsl:text>hide</xsl:text>
						</xsl:when>
						<xsl:otherwise>
							<xsl:value-of select="@class"/>
						</xsl:otherwise>
					</xsl:choose>
				</xsl:attribute>
			</xsl:if>
		</tr>
	</xsl:template>

	<!-- Normalize SDE's to use span elements -->
	<xsl:template match="p[@data-fact-id]">
		<span data-fact-id="{@data-fact-id}">
			<xsl:copy-of select="@*"/>
			<xsl:apply-templates/>
		</span>
	</xsl:template>

	<!-- Remove empty paragraphs -->
	<xsl:template match="p[not(*) and not(normalize-space()) and not(@data-fact-id)]"/>

	<!-- List items with nested div's -->
	<xsl:template match="li[div | *//div]">
		<li>
			<xsl:copy-of select="@*"/>
			<xsl:choose>
				<xsl:when test="*//div">
					<xsl:apply-templates select="*//div/node()"/>
				</xsl:when>
				<xsl:otherwise>
					<xsl:apply-templates select="div/node()"/>
				</xsl:otherwise>
			</xsl:choose>
		</li>
	</xsl:template>

	<!-- List items with nested p's -->
	<xsl:template match="li[p]">
		<li>
			<xsl:copy-of select="@*"/>
			<xsl:apply-templates select="p/node()"/>
		</li>
	</xsl:template>

	<!-- Table wrapper div corrections -->
	<xsl:template match="div[table]">
		
		<xsl:variable name="tableid">
			<xsl:choose>
				<xsl:when test="not(contains(@id, '_')) and string-length(@id) &gt; 0">
					<xsl:value-of select="@id"/>
				</xsl:when>
				<xsl:when test="not(contains(@id, '_[id]')) and string-length(@id) &gt; 0">
					<xsl:value-of select="substring-after(@id, 'tablewrapper_')"/>
				</xsl:when>
				<xsl:otherwise>
					<!-- Construct a unique id -->
					<xsl:variable name="uniquestring">
						<xsl:for-each select="div[@class = 'tablegraph-header-wrapper']//p">
							<xsl:value-of select="normalize-space(.)" disable-output-escaping="yes"/>
						</xsl:for-each>
					</xsl:variable>
					
					<xsl:variable name="uniquestringnospaces">
						<xsl:call-template name="string-replace-all">
							<xsl:with-param name="text" select="$uniquestring"/>
							<xsl:with-param name="search">
								<xsl:text> </xsl:text>
							</xsl:with-param>
							<xsl:with-param name="replace">
								<xsl:text/>
							</xsl:with-param>
						</xsl:call-template>
					</xsl:variable>
					
					<xsl:value-of select="$uniquestringnospaces"/>
				</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>
		
		
		<div id="tablewrapper_{$tableid}">
			<xsl:copy-of select="@*[not(local-name() = 'class' or local-name() = 'id')]"/>
			<xsl:if test="@class">
				<xsl:attribute name="class">
					<xsl:choose>
						<xsl:when test="contains(@class, 'tablewidth-100tablewrapper')">
							<xsl:call-template name="replace">
								<xsl:with-param name="text" select="@class"/>
								<xsl:with-param name="search">100table</xsl:with-param>
								<xsl:with-param name="replace">100 table</xsl:with-param>
							</xsl:call-template>
						</xsl:when>
						<xsl:when test="contains(@class, 'tablewidth-100contentblockstructured-data-table')">
							<xsl:call-template name="replace">
								<xsl:with-param name="text" select="@class"/>
								<xsl:with-param name="search">tablewidth-100contentblockstructured-data-table</xsl:with-param>
								<xsl:with-param name="replace">tablewidth-100 contentblock structured-data-table</xsl:with-param>
							</xsl:call-template>
						</xsl:when>
						<xsl:when test="contains(@class, 'contentblockstructured-data-table')">
							<xsl:call-template name="replace">
								<xsl:with-param name="text" select="@class"/>
								<xsl:with-param name="search">contentblockstructured-data-table</xsl:with-param>
								<xsl:with-param name="replace">contentblock structured-data-table</xsl:with-param>
							</xsl:call-template>
						</xsl:when>
						<xsl:when test="contains(@class, 'textcontentblock')">
							<xsl:call-template name="replace">
								<xsl:with-param name="text" select="@class"/>
								<xsl:with-param name="search">textcontentblock</xsl:with-param>
								<xsl:with-param name="replace">text contentblock</xsl:with-param>
							</xsl:call-template>
						</xsl:when>
						<xsl:when test="contains(@class, 'financialcontentblock')">
							<xsl:call-template name="replace">
								<xsl:with-param name="text" select="@class"/>
								<xsl:with-param name="search">financialcontentblock</xsl:with-param>
								<xsl:with-param name="replace">financial contentblock</xsl:with-param>
							</xsl:call-template>
						</xsl:when>
						<xsl:when test="contains(@class, 'tablewidth-100financial')">
							<xsl:call-template name="replace">
								<xsl:with-param name="text" select="@class"/>
								<xsl:with-param name="search">tablewidth-100financial</xsl:with-param>
								<xsl:with-param name="replace">tablewidth-100 financial</xsl:with-param>
							</xsl:call-template>
						</xsl:when>
						<xsl:when test="contains(@class, 'tablewidth-100contentblock')">
							<xsl:call-template name="replace">
								<xsl:with-param name="text" select="@class"/>
								<xsl:with-param name="search">tablewidth-100contentblock</xsl:with-param>
								<xsl:with-param name="replace">tablewidth-100 contentblock</xsl:with-param>
							</xsl:call-template>
						</xsl:when>
						<xsl:when test="contains(@class, 'contentblocktx-')">
							<xsl:call-template name="replace">
								<xsl:with-param name="text" select="@class"/>
								<xsl:with-param name="search">contentblocktx-</xsl:with-param>
								<xsl:with-param name="replace">contentblock tx-</xsl:with-param>
							</xsl:call-template>
						</xsl:when>
						<xsl:when test="contains(@class, 'tablewidth-100dataar')">
							<xsl:call-template name="replace">
								<xsl:with-param name="text" select="@class"/>
								<xsl:with-param name="search">tablewidth-100dataar</xsl:with-param>
								<xsl:with-param name="replace">tablewidth-100 dataar </xsl:with-param>
							</xsl:call-template>
						</xsl:when>
						<xsl:otherwise>
							<xsl:value-of select="@class"/>
						</xsl:otherwise>
					</xsl:choose>
				</xsl:attribute>
			</xsl:if>

			<xsl:for-each select="*">
				<xsl:choose>
					<xsl:when test="local-name() = 'table'">
						<table id="table_{$tableid}">
							<xsl:copy-of select="@*[not(local-name() = 'id')]"/>
							<xsl:apply-templates select="*"/>
						</table>
					</xsl:when>
					<xsl:otherwise>
						<xsl:apply-templates select="."/>
					</xsl:otherwise>
				</xsl:choose>
			</xsl:for-each>
		</div>
	</xsl:template>

	<!-- Utility to replace a single occurance of a string -->
	<xsl:template name="replace">
		<xsl:param name="text"/>
		<xsl:param name="search"/>
		<xsl:param name="replace"/>

		<xsl:choose>
			<xsl:when test="contains($text, $search)">
				<xsl:value-of select="substring-before($text, $search)"/>
				<xsl:value-of select="$replace"/>
				<xsl:value-of select="substring-after($text, $search)"/>
			</xsl:when>
			<xsl:otherwise>
				<xsl:value-of select="$text"/>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>

	<!-- Recursive routine for replacing multiple occurances of a string -->
	<xsl:template name="string-replace-all">
		<xsl:param name="text"/>
		<xsl:param name="search"/>
		<xsl:param name="replace"/>
		<xsl:choose>
			<xsl:when test="contains($text, $search)">
				<xsl:value-of select="substring-before($text, $search)"/>
				<xsl:value-of select="$replace"/>
				<xsl:call-template name="string-replace-all">
					<xsl:with-param name="text" select="substring-after($text, $search)"/>
					<xsl:with-param name="search" select="$search"/>
					<xsl:with-param name="replace" select="$replace"/>
				</xsl:call-template>
			</xsl:when>
			<xsl:otherwise>
				<xsl:value-of select="$text"/>
			</xsl:otherwise>
		</xsl:choose>
	</xsl:template>

</xsl:stylesheet>
