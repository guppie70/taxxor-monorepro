<?xml version='1.0'?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<xsl:param name="output_channel_name"/>
	<xsl:param name="output_channel_language"/>
	<xsl:param name="output_channel_type"/>
	<xsl:param name="editor-id">default_filing</xsl:param>
	<xsl:param name="item_id">0</xsl:param>
	<xsl:param name="all-permissions">all</xsl:param>
	<xsl:param name="show-section-numbers">no</xsl:param>
	<xsl:param name="context"/>

	<xsl:output method="html" omit-xml-declaration="yes"/>
	<xsl:strip-space elements="*"/>

	<xsl:variable name="newline">
		<xsl:text>
		</xsl:text>
	</xsl:variable>

	<xsl:template match="/">
		<ul class="nav nav-pills nav-stacked">
			<!--<xsl:comment>
				all-permissions: <xsl:value-of select="$all-permissions"/>
			</xsl:comment>-->
			<xsl:apply-templates select="/items/structured/item"/>
		</ul>
	</xsl:template>


	<xsl:template match="item">
		<xsl:param name="level">0</xsl:param>
		<xsl:variable name="next_level">
			<xsl:value-of select="number($level) + 1"/>
		</xsl:variable>

		<!--
				nav-list
			<xsl:if test="number($level)=0">
				<li class="nav-header"><xsl:value-of select="$output_channel_name"/></li>
			</xsl:if>
			-->
		<li class="level-{$level}">
			<xsl:attribute name="class">
				<xsl:text>level-</xsl:text>
				<xsl:value-of select="$level"/>
				<xsl:if test="string($item_id) = @id">
					<xsl:text> active</xsl:text>
				</xsl:if>
				<xsl:if test="@editable and @editable = 'false'">
					<xsl:text> not-editable</xsl:text>
				</xsl:if>
			</xsl:attribute>
			<!--  
					<xsl:if test="number($level)=0">
					<xsl:attribute name="style">margin-left:0px;</xsl:attribute>
					</xsl:if>
				-->
			<a href="" data-id="{@id}">
				<xsl:choose>
					<xsl:when test="$context = 'taxxoreditor' and number($level) = 0">
						<xsl:choose>
							<xsl:when test="$output_channel_language = 'nl'">
								<xsl:text>Volledige </xsl:text>
								<xsl:value-of select="$output_channel_name"/>
								<xsl:if test="$output_channel_type = 'pdf'">
									<xsl:text> </xsl:text>
									<small>(niet-bewerkbaar)</small>
								</xsl:if>
							</xsl:when>
							<xsl:otherwise>
								<xsl:text>Full </xsl:text>
								<xsl:value-of select="$output_channel_name"/>
								<xsl:if test="$output_channel_type='pdf'">
									<xsl:text> </xsl:text>
									<small>(not-editable)</small>
								</xsl:if>
							</xsl:otherwise>
						</xsl:choose>

					</xsl:when>
					<xsl:otherwise>
						<!-- Render a prefix in case we need to show a section number -->
						<xsl:if test="$show-section-numbers = 'yes' and @data-tocnumber">
							<small>
								<xsl:choose>
									<xsl:when test="contains(@data-tocnumber, 'note ')">
										<xsl:text>(</xsl:text>
										<xsl:value-of select="substring-after(@data-tocnumber, 'note ')"/>
										<xsl:text>)</xsl:text>
									</xsl:when>
									<xsl:otherwise>
										<xsl:value-of select="@data-tocnumber"/>
									</xsl:otherwise>
								</xsl:choose>							
							</small>
						</xsl:if>
						
						<xsl:value-of select="web_page/linkname"/>
						
					</xsl:otherwise>
				</xsl:choose>
			</a>

			<div id="item_{@id}" class="item_level{$level}">
				<!--<xsl:value-of select="@editable"/>-->
				<div class="item_data">
					<span class="item_attributes">
						<xsl:for-each select="@*">
							<span>
								<xsl:attribute name="class">
									<xsl:value-of select="local-name(.)"/>
								</xsl:attribute>
								<xsl:value-of select="."/>
							</span>
						</xsl:for-each>
					</span>
					<span class="system">
						<span class="data_src">
							<xsl:value-of select="@data-ref"/>
						</span>
						<span class="prms">
							<xsl:choose>
								<xsl:when test="count(permissions/permission[@id = 'all']) &gt; 0">
									<xsl:value-of select="$all-permissions"/>
								</xsl:when>
								<xsl:otherwise>
									<xsl:for-each select="permissions/permission">
										<xsl:value-of select="@id"/>
										<xsl:if test="position() != last()">,</xsl:if>
									</xsl:for-each>
								</xsl:otherwise>
							</xsl:choose>
						</span>
					</span>
				</div>
			</div>


			<xsl:if test="./sub_items">
				<ul class="nav nav-pills nav-stacked">
					<xsl:apply-templates select="./sub_items/item[not(@hidefromui = 'true')]">
						<xsl:with-param name="level" select="$next_level"/>
					</xsl:apply-templates>
				</ul>
			</xsl:if>
		</li>

	</xsl:template>

</xsl:stylesheet>
