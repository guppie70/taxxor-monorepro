<?xml version='1.0'?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

	<xsl:param name="first-hierarchical-level">1</xsl:param>

	<xsl:output method="xml" omit-xml-declaration="yes" indent="yes"/>
	
	<xsl:strip-space elements="*"/>


	<xsl:template match="@* | * | processing-instruction() | comment()">
		<xsl:copy>
			<xsl:apply-templates select="* | @* | text() | processing-instruction() | comment()"/>
		</xsl:copy>
	</xsl:template>


	<xsl:template match="/">
		<items>
			<structured>
				<xsl:apply-templates select="/items/structured/item"/>
			</structured>
			<xsl:if test="/items/unstructured">
				<unstructured>
					<xsl:apply-templates select="/items/unstructured/item"/>
				</unstructured>
			</xsl:if>
		</items>
	</xsl:template>




	<xsl:template match="item">
		<xsl:param name="prefix"/>
		<xsl:param name="level">0</xsl:param>
		<xsl:variable name="next_level">
			<xsl:value-of select="number($level) + 1"/>
		</xsl:variable>

		<xsl:variable name="position-correction">
			<xsl:choose>
				<xsl:when test="number($level) = number($first-hierarchical-level)">
					<xsl:value-of select="count(../item[@data-tocstart]/preceding-sibling::item)"/>
				</xsl:when>
				<xsl:otherwise>0</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>

		<xsl:variable name="in-toc-section">
			<xsl:choose>
				<xsl:when test="(preceding-sibling::item/@data-tocstart and (following::item/@data-tocend or descendant-or-self::item/@data-tocend)) or @data-tocstart or @data-tocend">
					<xsl:text>yes</xsl:text>
				</xsl:when>
				<xsl:otherwise>
					<xsl:text>no</xsl:text>
				</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>

		<xsl:variable name="set-header-number">
			<xsl:choose>
				<xsl:when test="number($level) = 0">no</xsl:when>
				<xsl:when test="$in-toc-section = 'yes'">
					<xsl:choose>
						<xsl:when test="web_page/linkname/text() = 'backcover'">
							<xsl:text>no</xsl:text>
						</xsl:when>
						<xsl:when test="contains(@id, 'back-cover')">
							<xsl:text>no</xsl:text>
						</xsl:when>
						<xsl:otherwise>
							<xsl:text>yes</xsl:text>
						</xsl:otherwise>
					</xsl:choose>
				</xsl:when>
				<xsl:when test="string-length($prefix) > 0">
					<xsl:text>yes</xsl:text>
				</xsl:when>
				<xsl:otherwise>no</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>

		<xsl:comment>position-correction: <xsl:value-of select="$position-correction"/>, set-header-number: <xsl:value-of select="$set-header-number"/>, in-toc-section: <xsl:value-of select="$in-toc-section"/>, prefix: <xsl:value-of select="$prefix"/></xsl:comment>
		
		<item id="{@id}" level="{$level}">

			<xsl:for-each select="@*[contains(local-name(), 'data-') and not(local-name() = 'data-tocnumber')]">
				<xsl:choose>
					<xsl:when test="local-name() = 'data-tocstyle' and $in-toc-section = 'no'"/>
					<xsl:otherwise>
						<xsl:apply-templates select="."/>
					</xsl:otherwise>
				</xsl:choose>
			</xsl:for-each>

			<xsl:if test="$set-header-number = 'yes'">
				<xsl:variable name="note-position">
					<xsl:choose>
						<xsl:when test="ancestor::item[contains(@data-tocstyle, 'notes')]">
							<xsl:value-of select="position()"/>
						</xsl:when>
						<xsl:when test="@data-tocstyle = 'notes123' or @data-tocstyle = 'notesabc'">
							<xsl:value-of select="number(1)"/>
						</xsl:when>
						<xsl:when test="preceding-sibling::item[contains(@data-tocstyle, 'notes')]">
							<xsl:value-of select="position() - count(parent::sub_items/item[following-sibling::item/@data-tocstart or following-sibling::item/@data-tocstyle])"/>
						</xsl:when>
						<xsl:otherwise>
							<xsl:value-of select="number(0)"/>
						</xsl:otherwise>
					</xsl:choose>
				</xsl:variable>


				<xsl:attribute name="data-tocnumber">
					<xsl:choose>
						<xsl:when test="ancestor::item[@data-tocstyle = 'nonumbering'] or @data-tocstyle = 'nonumbering' or preceding-sibling::item[@data-tocstyle = 'nonumbering'] or ../../preceding-sibling::item[@data-tocstyle = 'nonumbering']">
							<xsl:text/>
						</xsl:when>
						
						
						
						<xsl:when test="@data-tocstyle = 'notes123' or preceding-sibling::item[@data-tocstyle = 'notes123']">
							<xsl:text>note </xsl:text>
							<xsl:value-of select="$note-position"/>
						</xsl:when>
						
						
						<!-- nested notes -->
						<xsl:when test="ancestor::item[@data-tocstyle = 'notes123'] or ../../preceding-sibling::item[@data-tocstyle = 'notes123'] or ../../../../preceding-sibling::item[@data-tocstyle = 'notes123']">
							<xsl:variable name="debug">no</xsl:variable>
							<xsl:variable name="number-of-dots">
								<xsl:value-of select="string-length($prefix) - string-length(translate($prefix, '.', ''))"/>
							</xsl:variable>
							
							<xsl:variable name="prefix-corrected">
								<xsl:choose>
									<xsl:when test="$number-of-dots = 1">
										<xsl:value-of select="$prefix"/>
									</xsl:when>
									<xsl:when test="$number-of-dots = 2">
										<xsl:value-of select="$prefix"/>
										<!--<xsl:value-of select="substring-after($prefix, '.')"/>-->
									</xsl:when>
									<xsl:otherwise>
										<xsl:value-of select="substring-after(substring-after($prefix, '.'), '.')"/>
									</xsl:otherwise>
								</xsl:choose>
							</xsl:variable>
							
							<xsl:choose>
								<xsl:when test="$debug = 'yes'">
									<xsl:text>(prefix: </xsl:text>
									<xsl:value-of select="$prefix"/>
									<xsl:text>, prefix-corrected: </xsl:text>
									<xsl:value-of select="$prefix-corrected"/>
									<xsl:text>, dots: </xsl:text>
									<xsl:value-of select="$number-of-dots"/>
									<xsl:text>) note </xsl:text>
								</xsl:when>
								<xsl:otherwise>
									<xsl:text>note </xsl:text>
								</xsl:otherwise>
							</xsl:choose>
							
							<xsl:value-of select="concat($prefix-corrected, (position() - number($position-correction)))"/>
						</xsl:when>
						
						<xsl:when test="ancestor::item[@data-tocstyle = 'notesabc'] or @data-tocstyle = 'notesabc' or preceding-sibling::item[@data-tocstyle = 'notesabc']">
							<xsl:text>note </xsl:text>
							<xsl:choose>
								<xsl:when test="$note-position = 1">A</xsl:when>
								<xsl:when test="$note-position = 2">B</xsl:when>
								<xsl:when test="$note-position = 3">C</xsl:when>
								<xsl:when test="$note-position = 4">D</xsl:when>
								<xsl:when test="$note-position = 5">E</xsl:when>
								<xsl:when test="$note-position = 6">F</xsl:when>
								<xsl:when test="$note-position = 7">G</xsl:when>
								<xsl:when test="$note-position = 8">H</xsl:when>
								<xsl:when test="$note-position = 9">I</xsl:when>
								<xsl:when test="$note-position = 10">J</xsl:when>
								<xsl:when test="$note-position = 11">K</xsl:when>
								<xsl:when test="$note-position = 12">L</xsl:when>
								<xsl:when test="$note-position = 13">M</xsl:when>
								<xsl:when test="$note-position = 14">N</xsl:when>
								<xsl:when test="$note-position = 15">O</xsl:when>
								<xsl:when test="$note-position = 16">P</xsl:when>
								<xsl:when test="$note-position = 17">Q</xsl:when>
								<xsl:when test="$note-position = 18">R</xsl:when>
								<xsl:when test="$note-position = 19">S</xsl:when>
								<xsl:when test="$note-position = 20">T</xsl:when>
								<xsl:when test="$note-position = 21">U</xsl:when>
								<xsl:when test="$note-position = 22">V</xsl:when>
								<xsl:when test="$note-position = 23">W</xsl:when>
								<xsl:when test="$note-position = 24">X</xsl:when>
								<xsl:when test="$note-position = 25">Y</xsl:when>
								<xsl:when test="$note-position = 26">Z</xsl:when>
								<xsl:otherwise>.</xsl:otherwise>
							</xsl:choose>
						</xsl:when>
						
						<!-- nested notes -->
						<xsl:when test="ancestor::item[@data-tocstyle = 'notesabc'] or ../../preceding-sibling::item[@data-tocstyle = 'notesabc'] or ../../../../preceding-sibling::item[@data-tocstyle = 'notesabc']">
							<xsl:text>note </xsl:text>
							
							<xsl:variable name="prefix-letter">
								<xsl:choose>
									<xsl:when test="$prefix = 1">A</xsl:when>
									<xsl:when test="$prefix = 2">B</xsl:when>
									<xsl:when test="$prefix = 3">C</xsl:when>
									<xsl:when test="$prefix = 4">D</xsl:when>
									<xsl:when test="$prefix = 5">E</xsl:when>
									<xsl:when test="$prefix = 6">F</xsl:when>
									<xsl:when test="$prefix = 7">G</xsl:when>
									<xsl:when test="$prefix = 8">H</xsl:when>
									<xsl:when test="$prefix = 9">I</xsl:when>
									<xsl:when test="$prefix = 10">J</xsl:when>
									<xsl:when test="$prefix = 11">K</xsl:when>
									<xsl:when test="$prefix = 12">L</xsl:when>
									<xsl:when test="$prefix = 13">M</xsl:when>
									<xsl:when test="$prefix = 14">N</xsl:when>
									<xsl:when test="$prefix = 15">O</xsl:when>
									<xsl:when test="$prefix = 16">P</xsl:when>
									<xsl:when test="$prefix = 17">Q</xsl:when>
									<xsl:when test="$prefix = 18">R</xsl:when>
									<xsl:when test="$prefix = 19">S</xsl:when>
									<xsl:when test="$prefix = 20">T</xsl:when>
									<xsl:when test="$prefix = 21">U</xsl:when>
									<xsl:when test="$prefix = 22">V</xsl:when>
									<xsl:when test="$prefix = 23">W</xsl:when>
									<xsl:when test="$prefix = 24">X</xsl:when>
									<xsl:when test="$prefix = 25">Y</xsl:when>
									<xsl:when test="$prefix = 26">Z</xsl:when>
									<xsl:otherwise>.</xsl:otherwise>
								</xsl:choose>								
							</xsl:variable>
							
							<xsl:value-of select="concat($prefix-letter, '.', (position() - number($position-correction)))"/>
						</xsl:when>
						<xsl:otherwise>
							<xsl:value-of select="concat($prefix, (position() - number($position-correction)))"/>
						</xsl:otherwise>
					</xsl:choose>
				</xsl:attribute>
			</xsl:if>

			<xsl:apply-templates select="*[not(local-name() = 'sub_items')]"/>
			<xsl:if test="./sub_items">
				<sub_items>
					<xsl:apply-templates select="./sub_items/item">
						<xsl:with-param name="level" select="$next_level"/>
						<xsl:with-param name="prefix">
							<xsl:choose>
								<xsl:when test="number($level) = 1 and @data-tocend"/>
								<xsl:when test="$set-header-number = 'yes' or string-length($prefix) > 0">
									<xsl:value-of select="concat($prefix, (position() - number($position-correction)), '.')"/>
								</xsl:when>
							</xsl:choose>

						</xsl:with-param>
					</xsl:apply-templates>
				</sub_items>
			</xsl:if>
		</item>


	</xsl:template>
</xsl:stylesheet>
